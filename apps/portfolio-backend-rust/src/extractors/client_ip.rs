use std::net::{IpAddr, SocketAddr};
use axum::extract::{ConnectInfo, FromRequestParts};
use axum::http::request::Parts;
use crate::state::app_state::AppState;
use crate::state::root_state::RootState;

pub struct ClientIp(pub IpAddr);

impl FromRequestParts<RootState> for ClientIp {
    type Rejection = std::convert::Infallible;

    async fn from_request_parts(parts: &mut Parts, state: &RootState) -> Result<Self, Self::Rejection> {
        if let Some(header) = &state.app.app_config.ip_header
        {
            if let Some(ip) = parts
                .headers
                .get(header)
                .and_then(|v| v.to_str().ok())
                .and_then(|v| v.split(',').next())
                .and_then(|v| v.trim().parse::<IpAddr>().ok())
            {
                return Ok(ClientIp(ip));
            }
        }

        if let Some(ip) = parts
            .headers
            .get("x-real-ip")
            .and_then(|v| v.to_str().ok())
            .and_then(|v| v.trim().parse::<IpAddr>().ok())
        {
            return Ok(ClientIp(ip));
        }

        let socket_ip = parts
            .extensions
            .get::<ConnectInfo<SocketAddr>>()
            .map(|ConnectInfo(addr)| addr.ip())
            .unwrap_or(IpAddr::V4(std::net::Ipv4Addr::UNSPECIFIED));

        Ok(ClientIp(socket_ip))
    }
}