use std::fs;
use std::fs::read_dir;
use std::path::Path;
use crate::errors::post_errors::PostGenerationError;
use crate::models::{PostMeta};
use crate::models::post_asset::PostAsset;

pub fn collect_posts(post_dir: &Path) -> Result<Vec<PostMeta>, PostGenerationError> {
    let mut post_meta = Vec::new();

    let blog_entries = read_dir(post_dir)
        .map_err(|_| PostGenerationError::PostDirReadError(post_dir.to_path_buf()))?;

    for entry in blog_entries {
        let path = match entry {
            Ok(path) => {
                path.path()
            }
            Err(err) => {
                log::warn!("Failed to read entry in post directory: {:?}", err);
                continue;
            }
        };

        if !path.is_dir() {
            continue;
        }

        if !path.join(".meta").exists() {
            log::warn!("No meta file found for post: {:?}. Skipping the blog", path);
            continue;
        }

        if !path.join("index.md").exists() {
            log::warn!("No index.md file found for post: {:?}. Skipping the blog", path);
            continue;
        }

        let result = PostMeta::parse(path.as_path());
        match result {
            Ok(meta) => {
                let slug = meta.slug.clone();
                post_meta.push(meta);
                log::info!("Parsed post meta: {:?}", slug);
            },
            Err(err) => {
                log::warn!("Failed to parse post meta: {:?}", err);
                return Err(err);
            }
        }
    }

    Ok(post_meta)
}

/// This can be travesely called
pub fn collect_post_assets(asset_path: &Path) -> Result<Vec<PostAsset>, PostGenerationError> {
    let mut post_assets = Vec::new();

    if !asset_path.exists(){
        log::info!("No assets found for post: {:?}", asset_path);
        return Ok(post_assets);
    }

    if !asset_path.is_dir(){
        log::warn!("Assets path is not a directory: {:?}", asset_path);
        return Err(PostGenerationError::AssetsPathNotDir(asset_path.to_path_buf()));
    }

    let asset_entries = asset_path
        .read_dir()
        .map_err(|err| PostGenerationError::AssetsPathReadError(err))?;

    for each_asset_entry in asset_entries {
        let each_asset = {
            match each_asset_entry {
                Ok(entry) => {
                    entry
                }
                Err(err) => {
                    return Err(PostGenerationError::AssetFileReadError(err));
                }
            }
        };

        let path = each_asset.path();
        if path.is_dir(){
            let nested = collect_post_assets(&each_asset.path())?;
            post_assets.extend(nested);
            continue;
        }

        let file_content = fs::read(each_asset.path()).map_err(|err| PostGenerationError::AssetFileReadError(err))?;

        let file_type = mime_guess::from_path(&each_asset.path()).first_or_octet_stream();
        let file_name = each_asset.file_name().into_string().map_err(|_| PostGenerationError::IncompatibleAssetFileName(path))?;
        let ea = PostAsset {
            path: each_asset.path(),
            file_sha: sha256::digest(&file_content),
            file_content,
            file_type,
            file_name,
        };
        post_assets.push(ea);
    }

    return Ok(post_assets)
}