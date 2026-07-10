use crate::models::PostMeta;
use chrono::{DateTime, NaiveDate, Utc};
use serde::Serialize;
use crate::models::post::Post;

#[derive(Debug, Serialize)]
pub struct BlogSpec {
    pub generated: DateTime<Utc>,
    pub posts: Vec<BlogSpecPost>,
}

impl BlogSpec {
    pub fn from_post(posts: &[Post]) -> Self {
        Self {
            generated: Utc::now(),
            posts: posts.iter().map(|p| BlogSpecPost::from_post_meta(&p.post_meta)).collect(),
        }
    }
}

#[derive(Debug, Serialize)]
pub struct BlogSpecPost {
    pub title: String,
    pub slug: String,
    pub date: NaiveDate,
    pub published: bool,
    pub icon: Option<String>,
    pub tags: Vec<String>,
    pub excerpt: Option<String>,
    pub hash: String,
}

impl BlogSpecPost {
    pub fn from_post_meta(meta: &PostMeta) -> Self {
        let mut hash = meta.hash.clone();
        hash.truncate(6);
        Self {
            title: meta.title.clone(),
            slug: meta.slug.clone(),
            date: meta.date,
            published: meta.published,
            icon: meta.icon.clone(),
            tags: meta.tags.clone(),
            excerpt: meta.excerpt.clone(),
            hash
        }
    }
}