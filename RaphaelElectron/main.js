/**
 * Raphael — Lord of Wisdom
 * Electron Main Process
 * Grants media (mic) permissions and creates frameless window.
 */

const { app, BrowserWindow, ipcMain, session } = require('electron');
const path = require('path');

// Enable speech flags
app.commandLine.appendSwitch('enable-features', 'WebBluetooth,WebUSB');

let mainWindow;

function createWindow() {
    mainWindow = new BrowserWindow({
        width: 1280,
        height: 850,
        minWidth: 920,
        minHeight: 680,
        frame: false,            // Frameless — custom titlebar
        transparent: false,
        backgroundColor: '#020206',
        titleBarStyle: 'hidden',
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true,
            preload: path.join(__dirname, 'preload.js'),
            webSecurity: true,
            allowRunningInsecureContent: false,
        },
        icon: path.join(__dirname, 'assets', 'icon.png'),
        show: false,
    });

    mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));

    // Show once fully loaded (prevents white flash)
    mainWindow.once('ready-to-show', () => {
        mainWindow.show();
    });

    // DevTools in dev mode
    if (process.argv.includes('--dev')) {
        mainWindow.webContents.openDevTools({ mode: 'detach' });
    }

    mainWindow.on('closed', () => {
        mainWindow = null;
    });
}

// IPC: Window Controls (minimize, maximize, close)
ipcMain.on('window-minimize', () => {
    if (mainWindow) mainWindow.minimize();
});

ipcMain.on('window-maximize', () => {
    if (mainWindow) {
        if (mainWindow.isMaximized()) {
            mainWindow.unmaximize();
        } else {
            mainWindow.maximize();
        }
    }
});

ipcMain.on('window-close', () => {
    if (mainWindow) mainWindow.close();
});

app.whenReady().then(() => {
    // Automatically grant media permissions (microphone) for voice commands!
    session.defaultSession.setPermissionRequestHandler((webContents, permission, callback) => {
        const allowed = ['media', 'audioCapture', 'notifications', 'speechRecognition'];
        if (allowed.includes(permission)) {
            callback(true);
        } else {
            callback(true);
        }
    });

    session.defaultSession.setPermissionCheckHandler(() => true);

    createWindow();
});

app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
        createWindow();
    }
});
