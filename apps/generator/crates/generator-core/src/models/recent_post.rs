use serde::{Deserialize, Serialize};
use crate::models::PostMeta;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RecentPost{

    pub hash: String,
    pub date: String,
    pub icon: String,
    pub title: String,
    pub slug: String,

}

impl RecentPost {
    pub fn create(post_meta: &PostMeta) -> Self {
        let mut hash_value = post_meta.hash.clone();
        hash_value.truncate(6);
        RecentPost {
            hash: hash_value,
            date: post_meta.date.format("%Y-%m-%d").to_string(),
            icon: post_meta.icon.clone().unwrap_or(String::new()).clone(),
            title: post_meta.title.clone(),
            slug: post_meta.slug.clone(),
        }
    }
}