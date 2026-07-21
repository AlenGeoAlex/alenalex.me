use utoipa::OpenApi;

#[derive(OpenApi)]
#[openapi(
    paths(
        crate::routes::guestbook::create_entry_handler,

        crate::routes::guestbook::list_entries,

        crate::routes::guestbook_likes::create_like,
        crate::routes::guestbook_likes::delete_like,
    ),
    components(
        schemas(
            crate::routes::guestbook::CreateEntryRequest,
            crate::routes::guestbook::CreateEntryResponse,

            crate::routes::guestbook::ListEntriesResponse,
        )
    ),
    tags(
        (name = "guestbook", description = "Guestbook entries API")
    )
)]
pub struct ApiDoc;