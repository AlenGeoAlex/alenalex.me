use s3::{Bucket, Region};
use crate::config::GeneratorConfig;
use anyhow::Result;
use s3::creds::Credentials;
use s3::error::S3Error::HttpFailWithBody;
use crate::models::post_asset::PostAsset;
use crate::models::remote_asset::RemoteAsset;

pub struct AssetSyncerService {

    bucket : Box<Bucket>,
    public_endpoint: String,

}

impl AssetSyncerService {

    const META_KEY: &str = "file-metadata";

    pub fn create(
        generator_config: &GeneratorConfig
    ) -> Result<AssetSyncerService> {
        let bucket_name = generator_config.r2_bucket.clone();
        let Ok(credentials) = Credentials::new(
            Some(generator_config.r2_access_key.as_str()),
            Some(generator_config.r2_secret_key.as_str()),
            None,
            None,
            None
        ) else{
            return Err(anyhow::anyhow!("Failed to create credentials"));
        };

        let bucket = {
            match Bucket::new(
                bucket_name.as_str(),
                Region::R2 {
                    account_id: generator_config.r2_account_id.clone()
                },
                credentials
            ) {
                Ok(bucket) => bucket,
                Err(err) => {
                    return Err(anyhow::anyhow!("Failed to create bucket: {}", err));
                }
            }
        };

        Ok(AssetSyncerService {
            bucket,
            public_endpoint: generator_config.r2_public_url_base.clone(),
        })
    }

    pub async fn try_get_remote(&self,
                                post_slug: &str,
                                post_asset: &PostAsset
    ) -> Result<Option<RemoteAsset>>{
        let file_path =  format!("assets/hotlink-ok/{}/{}", post_slug, post_asset.file_name);
        let result = self.bucket.head_object(
            file_path.as_str()
        ).await;

        match result {
            Ok((obj, code)) => {
                if code == 404 {
                    return Ok(None)
                }

                let Some(metadata) = obj.metadata else {
                    log::warn!("Failed to get remote asset metadata, considering it as a different object");
                    return Ok(None);
                };

                match metadata.get(Self::META_KEY) {
                    None => {
                        log::warn!("Failed to get remote asset metadata, considering it as a different object");
                        Ok(None)
                    }
                    Some(meta) => {
                        if meta.eq(post_asset.file_sha.as_str())  {
                            log::info!("Remote asset is up to date {}", file_path);
                            return Ok(Some(RemoteAsset {
                                content_hash: meta.to_string(),
                                key: file_path.clone(),
                                url: format!("{}/{}",
                                             self.public_endpoint,
                                             file_path
                                )
                            }));
                        };

                        Ok(None)
                    }
                }
            },
            Err(err) => {
                match err {
                    HttpFailWithBody(code, str) => {
                        if code == 404 {
                            log::info!("Remote asset not found: {}", file_path);
                            return Ok(None)
                        }

                        Err(anyhow::anyhow!("Failed to upload asset: {:?}", str))
                    },
                    _ => Err(anyhow::anyhow!("Failed to upload asset: {:?}", err))
                }
            }
        }
    }

    pub async fn upload_asset(&self,
                              post_slug: &str,
                              post_asset: &PostAsset) -> anyhow::Result<RemoteAsset>
    {
        let file_path =  format!("assets/hotlink-ok/{}/{}", post_slug, post_asset.file_name);

        let builder = self.bucket.put_object_builder(file_path.as_str(), post_asset.file_content.as_ref())
            .with_content_type(post_asset.file_type.to_string().as_str())
            .with_metadata(Self::META_KEY, post_asset.file_sha.as_str())?;

        let builder_resp = builder.execute().await;
        match builder_resp {
            Ok(resp) => {
                let status_code = resp.status_code();
                if (200..300).contains(&status_code){
                    log::info!("Uploaded asset: {}", file_path);
                    return Ok(RemoteAsset {
                        content_hash: post_asset.file_sha.clone(),
                        key: file_path.clone(),
                        url: format!("{}/{}",
                                     self.public_endpoint,
                                     file_path
                        )
                    });
                };

                Err(anyhow::anyhow!("Failed to upload asset: {}", resp.status_code()))
            },
            Err(err) => {
                Err(anyhow::anyhow!("Failed to upload asset: {}", err))
            }
        }
    }
}