use std::path::Path;
use chrono::NaiveDate;
use serde::{Deserialize, Serialize};
use crate::errors::post_errors::PostGenerationError;
use crate::models::post_type::PostType;

#[derive(Debug, Clone, Deserialize)]
struct RawPostMeta {
    title: String,

    #[serde(rename = "page-title")]
    page_title: Option<String>,

    date: String,
    published: bool,
    icon: Option<String>,

    #[serde(default)]
    tags: Vec<String>,

    #[serde(rename = "type")]
    post_type: PostType,

    excerpt: Option<String>,

    og_image_asset: Option<String>,
}

#[derive(Debug, Clone)]
pub struct PostMeta {
    pub title: String,
    pub page_title: Option<String>,
    pub date: NaiveDate,
    pub published: bool,
    pub icon: Option<String>,
    pub tags: Vec<String>,
    pub post_type: PostType,
    pub excerpt: Option<String>,
    pub og_image_asset: Option<String>,

    // derived, always set at construction
    pub slug: String,
    pub hash: String,
    pub file: String,
}

impl PostMeta {

    pub fn parse(post_dir: &Path) -> Result<Self, PostGenerationError> {
        let meta_path = post_dir.join(".meta");
        if !meta_path.exists() {
            return Err(PostGenerationError::NoMetaFileFound(post_dir.to_path_buf()));
        }

        let contents = std::fs::read_to_string(&meta_path)
            .map_err(|e| PostGenerationError::FailedMetaFileRead(meta_path.clone(), e))?;

        let raw : RawPostMeta = serde_yaml::from_str(&contents)
            .map_err(|e| PostGenerationError::FailedMetaFileParse(meta_path.clone(), e))?;

        Self::from_raw(raw, post_dir)
    }

    fn from_raw(raw: RawPostMeta, post_dir: &Path) -> Result<Self, PostGenerationError>{
        let slug = post_dir
            .file_name()
            .ok_or(PostGenerationError::FailedPostSlugGeneration)?
            .to_string_lossy()
            .to_string();

        let hash = sha256::digest(slug.as_bytes());
        let file = format!("{}/index.md", post_dir.display());
        let date = NaiveDate::parse_from_str(raw.date.as_str(), "%Y-%m-%d")
            .map_err(|_| PostGenerationError::FailedDateParse(
                post_dir.to_string_lossy().to_string()
            ))?;
        Ok(Self {
            title: raw.title,
            page_title: raw.page_title,
            date,
            published: raw.published,
            icon: raw.icon,
            tags: raw.tags,
            post_type: raw.post_type,
            excerpt: raw.excerpt,
            og_image_asset: raw.og_image_asset,
            slug,
            hash,
            file,
        })
    }
}