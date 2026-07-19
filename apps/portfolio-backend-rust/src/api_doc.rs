use utoipa::OpenApi;

#[derive(OpenApi)]
#[openapi(
    paths(
        crate::routes::guestbook::create_entry_handler,

        crate::routes::guestbook::list_entries,
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