use std::net::IpAddr;
use sha2::{Digest, Sha256};

pub fn hash_ip(salt: &str, ip: IpAddr) -> String {
    let mut hasher = Sha256::new();
    hasher.update(salt.as_bytes());
    hasher.update(ip.to_string().as_bytes());
    let result = hasher.finalize();
    hex::encode(result)
}