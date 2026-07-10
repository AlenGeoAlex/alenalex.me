use mime_guess::Mime;
use std::path::PathBuf;

#[derive(Debug, Clone, Eq, PartialEq, Hash)]
pub struct PostAsset {
    pub file_name: String,
    pub path: PathBuf,
    pub file_sha: String,
    pub file_type: Mime,
    pub file_content: Vec<u8>,
}