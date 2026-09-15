// Keeps the console window from appearing behind the app on Windows.
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use tauri::{
    menu::{Menu, MenuItem},
    tray::TrayIconBuilder,
    Manager, WebviewWindow,
};
/* GlobalShortcutExt is what puts .global_shortcut() on the App handle. Without
   it in scope the method simply does not exist and the compiler says the type
   has no such method — the trait has to be imported even though nothing here
   names it directly. */
use tauri_plugin_global_shortcut::{Code, GlobalShortcutExt, Modifiers, Shortcut, ShortcutState};

/* The hotkey. A bare Ctrl cannot be registered on its own — every OS treats a
   lone modifier as part of another combination, never as a shortcut in itself,
   so nothing would ever fire. This is the nearest thing that actually works
   and does not collide with anything common. Change it here and in
   HOTKEY_LABEL below if you would rather have something else. */
const HOTKEY_MODS: Modifiers = Modifiers::CONTROL.union(Modifiers::SHIFT);
const HOTKEY_CODE: Code = Code::Space;
const HOTKEY_LABEL: &str = "Ctrl+Shift+Space";

/* Park the panel against the right-hand edge, vertically centred — the corner
   of the screen you are least likely to be working in. Done in code rather
   than as fixed coordinates in the config because the right edge depends on
   the monitor, and a hardcoded position lands off-screen on a different one. */
fn park_on_right_edge(window: &WebviewWindow) {
    if let Ok(Some(monitor)) = window.current_monitor() {
        let screen = monitor.size();
        let scale = monitor.scale_factor();
        if let Ok(win) = window.outer_size() {
            let margin = (24.0 * scale) as i32;
            let x = screen.width as i32 - win.width as i32 - margin;
            let y = (screen.height as i32 - win.height as i32) / 2;
            let _ = window.set_position(tauri::PhysicalPosition { x, y });
        }
    }
}

/* One key does both jobs: bring it up when it is away, put it away when it is
   there. A separate hide shortcut would be one more thing to remember, and the
   whole point is that this is reflexive. */
fn toggle(window: &WebviewWindow) {
    let visible = window.is_visible().unwrap_or(false);
    let focused = window.is_focused().unwrap_or(false);
    if visible && focused {
        let _ = window.hide();
    } else {
        let _ = window.show();
        let _ = window.set_focus();
    }
}

/* Closing the window you are looking at. This is the first thing JARVIS can
   do that reaches outside his own process, so it is written as narrowly as
   the capability allows: no arguments, no target selection, nothing the
   model can steer. It closes the foreground window and only that.

   WM_CLOSE is a request, not a kill. The application decides what to do with
   it — an unsaved document still prompts, exactly as clicking the X would.
   TerminateProcess would be the destructive version and is deliberately not
   used here.

   The guard that matters: refuse when the foreground window is our own.
   Without it, asking JARVIS to close a window while his own panel happens to
   have focus would close JARVIS — a confusing way to lose the assistant you
   were talking to. */
#[cfg(target_os = "windows")]
#[tauri::command]
fn close_foreground_window() -> Result<String, String> {
    use windows_sys::Win32::Foundation::HWND;
    use windows_sys::Win32::UI::WindowsAndMessaging::{
        GetForegroundWindow, GetWindowTextW, PostMessageW, WM_CLOSE,
    };

    unsafe {
        let hwnd: HWND = GetForegroundWindow();
        /* HWND is a raw pointer in windows-sys 0.59, not an integer, so it is
           compared against a null pointer rather than 0. */
        if hwnd.is_null() {
            return Err("no window is in the foreground".into());
        }

        // Read the title, both to report it back and to recognise ourselves.
        let mut buf = [0u16; 512];
        let len = GetWindowTextW(hwnd, buf.as_mut_ptr(), buf.len() as i32);
        let title = if len > 0 {
            String::from_utf16_lossy(&buf[..len as usize])
        } else {
            String::new()
        };

        if title == "JARVIS" || title.is_empty() {
            return Err("that is my own window — refusing to close it".into());
        }

        /* WPARAM and LPARAM are also pointer-sized newtypes here; 0 as _
           lets the compiler produce whichever zero value each one wants. */
        if PostMessageW(hwnd, WM_CLOSE, 0 as _, 0 as _) == 0 {
            return Err(format!("Windows refused to close \"{}\"", title));
        }
        Ok(title)
    }
}

#[cfg(not(target_os = "windows"))]
#[tauri::command]
fn close_foreground_window() -> Result<String, String> {
    Err("only available on Windows".into())
}

/* Closing a named window — in practice a browser window, since that is what
   accumulates. Searches the visible top-level windows for one whose title
   contains the text, and sends it the same close request as clicking the X.

   Two guards, both learned from the foreground version: our own window is
   never a candidate, and an empty search string is refused outright rather
   than matching the first window it finds. A close command that can be
   talked into matching everything is worse than no close command. */
#[cfg(target_os = "windows")]
#[tauri::command]
fn close_window_named(name: String) -> Result<String, String> {
    use windows_sys::Win32::Foundation::{BOOL, HWND, LPARAM};
    use windows_sys::Win32::UI::WindowsAndMessaging::{
        EnumWindows, GetWindowTextW, IsWindowVisible, PostMessageW, WM_CLOSE,
    };

    let needle = name.trim().to_lowercase();
    if needle.is_empty() {
        return Err("no window name given".into());
    }

    struct Search {
        needle: String,
        found: Option<(isize, String)>,
    }

    unsafe extern "system" fn visit(hwnd: HWND, lparam: LPARAM) -> BOOL {
        unsafe {
            let search = &mut *(lparam as *mut Search);
            if search.found.is_some() || IsWindowVisible(hwnd) == 0 {
                return 1;
            }
            let mut buf = [0u16; 512];
            let len = GetWindowTextW(hwnd, buf.as_mut_ptr(), buf.len() as i32);
            if len <= 0 {
                return 1;
            }
            let title = String::from_utf16_lossy(&buf[..len as usize]);
            if title == "JARVIS" {
                return 1; // never ourselves
            }
            if title.to_lowercase().contains(&search.needle) {
                search.found = Some((hwnd as isize, title));
                return 0; // stop enumerating
            }
            1
        }
    }

    let mut search = Search { needle: needle.clone(), found: None };
    unsafe {
        EnumWindows(Some(visit), &mut search as *mut Search as LPARAM);
    }

    match search.found {
        None => Err(format!("no open window matching \"{}\"", name)),
        Some((hwnd, title)) => unsafe {
            if PostMessageW(hwnd as HWND, WM_CLOSE, 0 as _, 0 as _) == 0 {
                Err(format!("Windows refused to close \"{}\"", title))
            } else {
                Ok(title)
            }
        },
    }
}

#[cfg(not(target_os = "windows"))]
#[tauri::command]
fn close_window_named(_name: String) -> Result<String, String> {
    Err("only available on Windows".into())
}

/* Closing a single browser tab. This is a different class of action from
   everything else here: WM_CLOSE politely ASKS a window to close, whereas a
   tab has no window of its own, so the only way in is to focus the browser
   and send it Ctrl+W — synthetic keystrokes aimed at whatever happens to be
   in front.

   That is what makes it risky, and what the guard below is for. If focus
   moves between finding the window and sending the keys — a notification
   steals it, the user clicks something — Ctrl+W lands somewhere else, and in
   an editor that closes a file. So focus is set, then VERIFIED, and the
   keystroke is only sent if the intended window really is in front. If it
   is not, nothing is sent and the caller is told. */
#[cfg(target_os = "windows")]
#[tauri::command]
fn close_browser_tab(name: String) -> Result<String, String> {
    use std::{thread, time::Duration};
    use windows_sys::Win32::Foundation::{BOOL, HWND, LPARAM};
    use windows_sys::Win32::UI::Input::KeyboardAndMouse::{
        SendInput, INPUT, INPUT_KEYBOARD, KEYBDINPUT, KEYEVENTF_KEYUP, VIRTUAL_KEY,
        VK_CONTROL, VK_W,
    };
    use windows_sys::Win32::UI::WindowsAndMessaging::{
        EnumWindows, GetForegroundWindow, GetWindowTextW, IsWindowVisible, SetForegroundWindow,
    };

    let needle = name.trim().to_lowercase();
    if needle.is_empty() {
        return Err("no window name given".into());
    }

    struct Search { needle: String, found: Option<(isize, String)> }

    unsafe extern "system" fn visit(hwnd: HWND, lparam: LPARAM) -> BOOL {
        unsafe {
            let search = &mut *(lparam as *mut Search);
            if search.found.is_some() || IsWindowVisible(hwnd) == 0 { return 1; }
            let mut buf = [0u16; 512];
            let len = GetWindowTextW(hwnd, buf.as_mut_ptr(), buf.len() as i32);
            if len <= 0 { return 1; }
            let title = String::from_utf16_lossy(&buf[..len as usize]);
            if title == "JARVIS" { return 1; }
            if title.to_lowercase().contains(&search.needle) {
                search.found = Some((hwnd as isize, title));
                return 0;
            }
            1
        }
    }

    let mut search = Search { needle, found: None };
    unsafe { EnumWindows(Some(visit), &mut search as *mut Search as LPARAM); }

    let (hwnd, title) = search.found.ok_or_else(|| format!("no open window matching \"{}\"", name))?;

    unsafe {
        SetForegroundWindow(hwnd as HWND);
        thread::sleep(Duration::from_millis(120));

        /* The guard. Without it a stolen focus turns this into a keystroke
           fired blindly at someone else's window. */
        if GetForegroundWindow() != hwnd as HWND {
            return Err(format!(
                "could not bring \"{}\" to the front, so nothing was sent",
                title
            ));
        }

        let key = |vk: VIRTUAL_KEY, up: bool| INPUT {
            r#type: INPUT_KEYBOARD,
            Anonymous: windows_sys::Win32::UI::Input::KeyboardAndMouse::INPUT_0 {
                ki: KEYBDINPUT {
                    wVk: vk,
                    wScan: 0,
                    dwFlags: if up { KEYEVENTF_KEYUP } else { 0 },
                    time: 0,
                    dwExtraInfo: 0,
                },
            },
        };

        let mut inputs = [
            key(VK_CONTROL, false),
            key(VK_W, false),
            key(VK_W, true),
            key(VK_CONTROL, true),
        ];
        let sent = SendInput(
            inputs.len() as u32,
            inputs.as_mut_ptr(),
            std::mem::size_of::<INPUT>() as i32,
        );
        if sent == 0 {
            return Err("Windows rejected the keystroke".into());
        }
    }

    Ok(title)
}

#[cfg(not(target_os = "windows"))]
#[tauri::command]
fn close_browser_tab(_name: String) -> Result<String, String> {
    Err("only available on Windows".into())
}

/* Screen capture. The point is not to save a file — it is to let him SEE
   what you are looking at, so the image goes back as base64 and straight
   into the conversation as an attachment. He already handles images; this
   just gives him eyes on your screen when you ask for them.

   Captures the primary monitor only. Multi-monitor selection would need a
   way to say which one, and there is no obvious vocabulary for that by
   voice — "the left one" means nothing to a display index. */
#[tauri::command]
fn take_screenshot() -> Result<String, String> {
    use base64::{engine::general_purpose::STANDARD, Engine};
    use std::io::Cursor;
    use dirs_next;
    use xcap::Monitor;

    let monitors = Monitor::all().map_err(|e| format!("could not list monitors: {e}"))?;
    let monitor = monitors
        .into_iter()
        .find(|m| m.is_primary())
        .ok_or_else(|| "no primary monitor found".to_string())?;

    let image = monitor
        .capture_image()
        .map_err(|e| format!("capture failed: {e}"))?;

    /* Scaled down before encoding. A 4K screenshot is several megabytes of
       PNG, which is slow to encode, slow to upload and far more detail than
       a vision model needs to read what is on screen. 1600px wide keeps text
       legible while keeping the payload small. */
    let (w, h) = (image.width(), image.height());
    let image = if w > 1600 {
        let nh = (h as f32 * (1600.0 / w as f32)) as u32;
        image::imageops::resize(&image, 1600, nh, image::imageops::FilterType::Triangle)
    } else {
        image
    };

    let mut buf = Cursor::new(Vec::new());
    image
        .write_to(&mut buf, image::ImageFormat::Png)
        .map_err(|e| format!("could not encode the image: {e}"))?;
    let bytes = buf.into_inner();

    /* Saved where Windows puts its own screenshots, so it turns up in the
       Photos gallery alongside them rather than somewhere only this app
       knows about. Failing to save is not fatal — being able to SEE the
       screen is the point, and a read-only folder should not cost that. */
    if let Some(dir) = dirs_next::picture_dir() {
        let shots = dir.join("Screenshots");
        let _ = std::fs::create_dir_all(&shots);
        let name = format!(
            "jarvis-{}.png",
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .map(|d| d.as_secs())
                .unwrap_or(0)
        );
        let _ = std::fs::write(shots.join(name), &bytes);
    }

    Ok(STANDARD.encode(bytes))
}

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![close_foreground_window, close_window_named, close_browser_tab, take_screenshot])
        .setup(|app| {
            let window = app
                .get_webview_window("main")
                .expect("the main window is missing from tauri.conf.json");

            park_on_right_edge(&window);
            let _ = window.show();
            let _ = window.set_focus();

            /* Registered through the native plugin, not a keydown listener in
               the page. A listener in the webview only fires while the window
               already has focus, which is useless here — the entire purpose is
               to summon it from whatever you were doing instead. */
            let shortcut = Shortcut::new(Some(HOTKEY_MODS), HOTKEY_CODE);
            let hotkey_window = window.clone();
            app.handle().plugin(
                tauri_plugin_global_shortcut::Builder::new()
                    .with_handler(move |_app, fired, event| {
                        // Pressed only: without this it toggles twice per press.
                        if event.state() == ShortcutState::Pressed
                            && fired.matches(HOTKEY_MODS, HOTKEY_CODE)
                        {
                            toggle(&hotkey_window);
                        }
                    })
                    .build(),
            )?;

            if let Err(err) = app.global_shortcut().register(shortcut) {
                // Another application may already own this combination. Say so
                // rather than leaving a hotkey that silently does nothing.
                eprintln!(
                    "JARVIS: could not register {} — another app may already use it ({})",
                    HOTKEY_LABEL, err
                );
            }

            /* A tray icon, because the window has no title bar and is hidden
               half the time: without it there is no way to get the app back if
               the shortcut is taken, and no obvious way to quit. */
            let show_item = MenuItem::with_id(app, "show", "Show JARVIS", true, None::<&str>)?;
            let hide_item = MenuItem::with_id(app, "hide", "Hide", true, None::<&str>)?;
            let quit_item = MenuItem::with_id(app, "quit", "Quit", true, None::<&str>)?;
            let menu = Menu::with_items(app, &[&show_item, &hide_item, &quit_item])?;

            let tray_window = window.clone();
            TrayIconBuilder::new()
                .icon(app.default_window_icon().unwrap().clone())
                .tooltip(&format!("JARVIS — {}", HOTKEY_LABEL))
                .menu(&menu)
                .show_menu_on_left_click(false)
                .on_menu_event(move |app, event| match event.id.as_ref() {
                    "show" => {
                        let _ = tray_window.show();
                        let _ = tray_window.set_focus();
                    }
                    "hide" => {
                        let _ = tray_window.hide();
                    }
                    "quit" => app.exit(0),
                    _ => {}
                })
                .build(app)?;

            Ok(())
        })
        .on_window_event(|window, event| {
            /* Closing the panel should put it away, not end the session — the
               conversation and the unlocked PIN live in the page, and killing
               the process throws both away. Quit is on the tray menu. */
            if let tauri::WindowEvent::CloseRequested { api, .. } = event {
                api.prevent_close();
                let _ = window.hide();
            }
        })
        .run(tauri::generate_context!())
        .expect("error while running JARVIS");
}
