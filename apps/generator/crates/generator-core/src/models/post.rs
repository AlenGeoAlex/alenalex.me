use crate::models::post_asset::PostAsset;
use crate::models::PostMeta;

#[derive(Debug, Clone)]
pub struct Post{
    pub post_meta: PostMeta,
    pub post_assets: Vec<PostAsset>,
    pub post_content: String,
}