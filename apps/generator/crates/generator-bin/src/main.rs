pub mod pipeline;

use futures::future::join_all;
use std::fs;
use std::path::Path;
use std::sync::Arc;
use anyhow::{anyhow, Context};
use anyhow::Result;
use generator_core::models::post::Post;
use generator_core::config::GeneratorConfig;
use generator_core::models::blog_spec::BlogSpec;
use generator_core::models::post_asset::PostAsset;
use generator_core::models::recent_post::RecentPost;
use generator_core::models::remote_asset::RemoteAsset;
use generator_core::services::asset_syncer_service::AssetSyncerService;

#[tokio::main]
async fn main() -> Result<()> {
    dotenv::dotenv().ok();
    env_logger::init();
    let gen_config = Arc::new(
        GeneratorConfig::from_env()
            .expect("Failed to load generator configuration from environment variables")
    );
    let asset_sync_service = Arc::new(AssetSyncerService::create(gen_config.as_ref())?);

    log::info!("Starting generator at {}", gen_config.posts_dir);

    let mut all_posts = get_posts(gen_config.as_ref())?;
    all_posts = get_posts_to_change(all_posts, &gen_config.clone().changed_post_dirs);
    all_posts.sort_by(|a, b| b.post_meta.date.cmp(&a.post_meta.date));
    if all_posts.len() == 0 {
        log::info!("No posts found. Bye bye");
        return Ok(());
    }

    let posts = all_posts.clone();

    for post in all_posts {
        start_pipeline(&gen_config, asset_sync_service.as_ref(), &post).await?
    }

    write_injected_constants(&posts, gen_config.as_ref())?;
    generate_blog_spec(&posts, gen_config.blogspec_output.clone())?;
    Ok(())
}

fn write_injected_constants(
    all_posts: &Vec<Post>,
    generator_config: &GeneratorConfig
) -> Result<()>
{
    let recent_posts = all_posts.iter()
        .take(5)
        .map(|post| RecentPost::create(&post.post_meta))
        .collect::<Vec<RecentPost>>();
    let script_file = Path::new(&generator_config.script_output).join("writings.constants.js");
    let json_string = serde_json::to_string(&recent_posts)?;
    let js_script = format!("const INJECTED_WRITINGS = {}", json_string);

    match fs::write(&script_file, js_script) {
        Err(err) => {
            Err(err).context("Error while writing injected constants")
        },
        _ => {
            log::info!("Injected constants written to {}", script_file.to_string_lossy());
            Ok(())
        }
    }
}

async fn start_pipeline(
    gen_config: &GeneratorConfig,
    asset_sync_service: &AssetSyncerService,
    posts: &Post
) -> Result<()> {
    let post_content = {
        match generator_core::services::markdown_service::walk(
            posts,
            gen_config
        ) {
            Ok(content) => content,
            Err(err) => {
                return Err(err).context("Error while walking markdown");
            }
        }
    };

    let mut total_assets = post_content.1;

    if let Some(og_image) = posts.post_meta.og_image_asset.as_ref() {
        match posts.post_assets.iter().find(|asset| asset.file_name.eq( og_image)) {
            None => {
                return Err(anyhow!("OG image asset not found for {}", posts.post_meta.slug));
            }
            Some(og_asset) => {
                total_assets.push(og_asset.clone());
            }
        }
    }


    let sync_futures = total_assets.iter().map(|asset| {
        sync_one_asset(&asset_sync_service, asset, &posts)
    });

    let results: Vec<anyhow::Result<RemoteAsset>> = join_all(sync_futures).await;

    let mut remote_assets = Vec::new();
    for result in results {
        remote_assets.push(result.context("Error while syncing asset")?);
    }

    match generator_core::services::html_renderer::render_html(
        &posts,
        &post_content.0,
        gen_config
    ) {
        Ok(path) => {
            log::info!("HTML rendered successfully at {}", path.to_string_lossy());
            Ok(())
        }
        Err(err) => {
            Err(err).context("Error while rendering HTML")
        }
    }
}

async fn sync_one_asset(
    syncer: &AssetSyncerService,
    post_asset: &PostAsset,
    post: &Post) -> Result<(RemoteAsset)> {
    match syncer.try_get_remote(&post.post_meta.slug, &post_asset).await {
        Ok(None) => {
            log::info!("Remote asset not found for {}", &post_asset.file_name);
            syncer.upload_asset(&post.post_meta.slug, &post_asset).await
        }
        Ok(Some(asset)) => Ok(asset),
        Err(err) => return Err(err).context("Error while getting remote asset")
    }
}



fn get_posts_to_change(
    all_posts: Vec<Post>,
    changed_post_dirs: &Option<Vec<String>>
) -> Vec<Post>{
    if let Some(dirs) = changed_post_dirs {
        if dirs.iter().any(|dir| dir == "*"){
            log::info!("All posts will be changed as wildcard is provided");
            return all_posts;
        }

        all_posts
            .iter()
            .filter(|post| dirs.contains(&post.post_meta.slug))
            .cloned()
            .collect()
    }else{
        all_posts
    }
}

fn get_posts(gen_config: &GeneratorConfig) -> Result<Vec<Post>> {
    let post_metas = {
        match generator_core::services::post_collector::collect_posts(
            Path::new(gen_config.posts_dir.as_str())
        ) {
            Ok(posts) => posts,
            Err(err) => {
                return Err(anyhow!(err));
            }
        }
    };

    let mut posts : Vec<Post> = Vec::new();
    for mut each_post in post_metas {
        let post_slug = &each_post.slug;
        let post_dir_buf = Path::new(gen_config.posts_dir.as_str())
            .join(post_slug);
        let posts_assets = {
            match generator_core::services::post_collector::collect_post_assets(
                post_dir_buf
                    .join("assets")
                    .as_path()
            ) {
                Ok(assets_of_post) => {
                    assets_of_post
                }
                Err(err) => {
                    return Err(anyhow!("Failed to collect assets for post {}: {}", each_post.slug, err));
                }
            }
        };

        let post_content = {
            match fs::read_to_string(post_dir_buf.join("index.md")) {
                Ok(content) => content,
                Err(_) => {
                    panic!("Failed to read post content for post {}", each_post.slug);
                }
            }
        };

        if let None = each_post.excerpt {
            log::info!("Deriving excerpt for post {}", each_post.slug);
            each_post.excerpt = generator_core::services::markdown_service::derive_excerpt(&post_content);
        }

        let post = Post {
            post_meta: each_post,
            post_assets: posts_assets,
            post_content
        };

        log::info!("Post {} collected", post.post_meta.slug);
        posts.push(post);
    }

    Ok(posts)
}

pub fn generate_blog_spec(posts: &[Post], output_path: String) -> anyhow::Result<()> {
    let spec = BlogSpec::from_post(posts);

    let json = serde_json::to_string_pretty(&spec)
        .context("Failed to serialize blog spec to JSON")?;

    fs::write(&output_path, json)
        .with_context(|| format!("Failed to write blog spec to {}", &output_path))?;

    log::info!("Blog spec written to {}", &output_path);
    Ok(())
}