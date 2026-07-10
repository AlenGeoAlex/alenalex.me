use std::io::Error;
use std::path::PathBuf;

#[derive(Debug, thiserror::Error)]
pub enum PostGenerationError {
    #[error("Failed to generate post slug")]
    FailedPostSlugGeneration,

    #[error("No meta file found in {0}")]
    NoMetaFileFound(PathBuf),

    #[error("failed to read meta file at {0:?}")]
    FailedMetaFileRead(PathBuf, #[source] std::io::Error),

    #[error("failed to parse meta file at {0:?}")]
    FailedMetaFileParse(PathBuf, #[source] serde_yaml::Error),

    #[error("Failed to read the directory of {0}")]
    PostDirReadError(PathBuf),

    #[error("Assets path is not a directory: {0}")]
    AssetsPathNotDir(PathBuf),

    #[error("Failed to read assets path: {0}")]
    AssetsPathReadError(Error),

    #[error("Failed to read asset file: {0}")]
    AssetFileReadError(Error),

    #[error("Asset file name is not valid UTF-8: {0}")]
    IncompatibleAssetFileName(PathBuf),

    #[error("Failed to parse date in post meta: {0}")]
    FailedDateParse(String),
}

impl PostGenerationError {
}
