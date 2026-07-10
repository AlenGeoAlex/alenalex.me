use std::fmt::format;
use std::sync::Arc;
use anyhow::Context;
use s3::Bucket;
use crate::config::GeneratorConfig;
use crate::models::blog_spec::BlogSpec;
use crate::models::post::Post;

pub struct BlogSpecService{
    pub bucket : Arc<Box<Bucket>>,
}

impl BlogSpecService {
    pub async fn update_blog_spec(
        &self,
        posts: &[Post]
    ) -> anyhow::Result<()>{
        let spec = BlogSpec::from_post(posts);

        let json = serde_json::to_string_pretty(&spec)
            .context("Failed to serialize blog spec to JSON")?;

        self.bucket
            .put_object_builder(format!("{}/.blogspec.json", GeneratorConfig::ASSET_KEY_PREFIX), json.as_bytes())
            .with_content_type("application/json")
            .execute()
            .await
            .context("Failed to upload blog spec to S3")?;

        log::info!("Blog spec uploaded successfully");
        Ok(())
    }

}