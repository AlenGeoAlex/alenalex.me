use std::collections::HashMap;
use std::fs;
use std::path::{Path, PathBuf};
use crate::config::GeneratorConfig;
use crate::models::post::Post;
use anyhow::{Context, Result};


pub fn render_html(
    post: &Post,
    html_string: &str,
    generator_config: &GeneratorConfig
) -> Result<PathBuf>{
    let meta = &post.post_meta;

    match fs::exists(&generator_config.template_path) {
        Ok(exists) => {
            if !exists {
                return Err(anyhow::anyhow!("Template file does not exist"));
            }
        }
        Err(err) => {
            return Err(err.into());
        }
    }

    let template = fs::read_to_string(&generator_config.template_path)?;
    let og_image = meta.og_image_asset
        .as_ref()
        .map(|asset| format!("{}/{}/{}/{}", generator_config.r2_public_url_base, GeneratorConfig::ASSET_KEY_PREFIX,&post.post_meta.slug, asset));
    let mut  truncated_hash = meta.hash.clone();
    truncated_hash.truncate(10);

    let mut tokens: HashMap<&str, String> = HashMap::new();
    tokens.insert("PAGE_TITLE", meta.page_title.clone().unwrap_or_else(|| meta.title.clone()));
    tokens.insert("EXCERPT", meta.excerpt.clone().unwrap_or_default());
    tokens.insert("SLUG", meta.slug.clone());
    tokens.insert("ICON", meta.icon.clone().unwrap_or_default());
    tokens.insert("TITLE", meta.title.clone());
    tokens.insert("DATE", meta.date.format("%Y-%m-%d").to_string());
    tokens.insert("HASH", truncated_hash);
    tokens.insert("TAGS_HTML", render_tags_html(&meta.tags));
    tokens.insert("CONTENT_HTML", html_string.to_string());
    tokens.insert("OG_IMAGE_URL", og_image.unwrap_or(format!("{}/{}/default_og.png", generator_config.r2_public_url_base, GeneratorConfig::ASSET_KEY_PREFIX)).to_string());

    let mut output = template;
    for (key, value) in &tokens {
        let placeholder = format!("{{{{{}}}}}", key);
        output = output.replace(&placeholder, value);
    }

    let post_dir = Path::new(generator_config.posts_output_dir.as_str()).join(&meta.slug);
    fs::create_dir_all(&post_dir)
        .with_context(|| format!("Failed to create output dir for {}", meta.slug))?;

    let output_path = post_dir.join("index.html");
    fs::write(&output_path, output)
        .with_context(|| format!("Failed to write HTML for {}", meta.slug))?;

    Ok(output_path)
}

fn render_tags_html(tags: &[String]) -> String {
    tags.iter()
        .map(|t| format!(r#"<span class="post-byline-tag">{}</span>"#, t))
        .collect::<Vec<_>>()
        .join("\n")
}