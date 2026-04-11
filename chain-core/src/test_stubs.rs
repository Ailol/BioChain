/// FFI stubs for SpacetimeDB symbols, only linked in native test builds.
/// These allow `cargo test` to link on x86_64 while real WASM builds use
/// the SpacetimeDB runtime's actual implementations.

unsafe extern "C" {
    // These are declared in spacetimedb_bindings_sys but only exist in the WASM runtime.
    // We provide dummy implementations here so the native test linker is satisfied.
}

#[unsafe(no_mangle)]
pub extern "C" fn index_id_from_name(_a: u32, _b: u32, _c: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn datastore_insert_bsatn(_a: u32, _b: u32, _c: u32, _d: u32, _e: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn datastore_update_bsatn(_a: u32, _b: u32, _c: u32, _d: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn bytes_sink_write(_a: u32, _b: u32, _c: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn bytes_source_remaining_length(_a: u32) -> u64 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn bytes_source_read(_a: u32, _b: u32, _c: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn console_timer_start(_a: u32, _b: u32) -> u32 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn console_log(_a: u32, _b: u32, _c: u32, _d: u32, _e: u32, _f: u32, _g: u32) {}
#[unsafe(no_mangle)]
pub extern "C" fn table_id_from_name(_a: u32, _b: u32, _c: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn get_jwt(_a: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn identity(_a: u32) {}
#[unsafe(no_mangle)]
pub extern "C" fn console_timer_end(_a: u32) {}
#[unsafe(no_mangle)]
pub extern "C" fn datastore_table_scan_bsatn(_a: u32, _b: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn datastore_index_scan_point_bsatn(_a: u32, _b: u32, _c: u32, _d: u32, _e: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn datastore_index_scan_range_bsatn(_a: u32, _b: u32, _c: u32, _d: u32, _e: u32, _f: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn datastore_delete_by_index_scan_point_bsatn(_a: u32, _b: u32, _c: u32, _d: u32, _e: u32, _f: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn datastore_delete_by_index_scan_range_bsatn(_a: u32, _b: u32, _c: u32, _d: u32, _e: u32, _f: u32, _g: u32) -> u16 { 0 }
#[unsafe(no_mangle)]
pub extern "C" fn row_iter_bsatn_advance(_a: u32, _b: u32, _c: u32) -> i16 { -1 }
#[unsafe(no_mangle)]
pub extern "C" fn row_iter_bsatn_close(_a: u32) {}
