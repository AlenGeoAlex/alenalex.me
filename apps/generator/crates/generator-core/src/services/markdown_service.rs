use std::borrow::Cow;
use comrak::{parse_document, Arena, Options};
use comrak::nodes::{AstNode, NodeValue};
use crate::config::GeneratorConfig;
use crate::models::post::Post;
use crate::models::post_asset::PostAsset;

pub fn walk(
    post: &Post,
    generator_config: &GeneratorConfig
) ->  anyhow::Result<(String, Vec<PostAsset>)>
{
    let arena = Arena::new();
    let options = comrak::Options::default();

    let doc = parse_document(&arena, post.post_content.as_str(), &options);

    let mut used_assets: Vec<PostAsset> = Vec::new();
    let mut broken: Vec<(&AstNode, String)> = Vec::new();

    for node in doc.descendants() {
        let mut data = node.data.borrow_mut();
        if let NodeValue::Image(link) = &mut data.value {
            let Some(filename) = link.url.strip_prefix("./assets/") else {
                continue; // not an assets/ reference at all — leave untouched
            };

            match post.post_assets.iter().find(|a| a.file_name == filename) {
                Some(asset) => {
                    link.url = format!(
                        "{}/{}/{}/{}",
                        generator_config.r2_public_url_base,
                        GeneratorConfig::ASSET_KEY_PREFIX,
                        post.post_meta.slug,
                        asset.file_name
                    );
                    used_assets.push(asset.clone());
                }
                None => {
                    log::warn!(
                        "Post '{}' references missing asset '{}'",
                        post.post_meta.slug, filename
                    );
                    drop(data); // release the borrow before touching node's children next
                    let alt_text = extract_alt_text(node);
                    broken.push((node, alt_text));
                }
            }
        }
    }

    for (node, alt_text) in broken {
        let text_node = arena.alloc(AstNode::from(NodeValue::Text(Cow::from(alt_text))));
        node.insert_before(text_node);
        node.detach();
    }

    let mut html = String::new();
    comrak::format_html(doc, &options, &mut html)?;

    Ok((html, used_assets))
}

pub fn derive_excerpt(markdown_body: &str) -> Option<String> {
    let arena = Arena::new();
    let options = Options::default();
    let root = parse_document(&arena, markdown_body, &options);

    for node in root.descendants() {
        let data = node.data.borrow();
        if let NodeValue::Paragraph = data.value {
            drop(data);
            let text = extract_text_content(node);
            let trimmed = text.trim();
            if !trimmed.is_empty() {
                return Some(truncate_excerpt(trimmed, 120));
            }
        }
    }

    None
}

fn extract_text_content<'a>(node: &'a AstNode<'a>) -> String {
    let mut text = String::new();
    for child in node.children() {
        if let NodeValue::Text(ref s) = child.data.borrow().value {
            text.push_str(s);
        }
    }
    text
}

fn truncate_excerpt(text: &str, max_len: usize) -> String {
    if text.chars().count() <= max_len {
        return text.to_string();
    }

    let truncated: String = text.chars().take(max_len).collect();
    match truncated.rfind(' ') {
        Some(idx) => format!("{}…", &truncated[..idx]),
        None => format!("{}…", truncated),
    }
}

fn extract_alt_text<'a>(image_node: &'a AstNode<'a>) -> String {
    let mut text = String::new();
    for child in image_node.children() {
        if let NodeValue::Text(ref s) = child.data.borrow().value {
            text.push_str(s);
        }
    }
    text
}
