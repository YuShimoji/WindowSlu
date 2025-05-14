import sys
import win32gui
import win32con
import win32api
from PyQt6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout,
                             QHBoxLayout, QSlider, QLabel, QPushButton,
                             QListWidget, QListWidgetItem, QTabWidget, QSystemTrayIcon,
                             QMenu, QStyle)
from PyQt6.QtCore import Qt, QPoint
from PyQt6.QtGui import QIcon, QAction, QCursor

class WindowSlu(QMainWindow):
    def __init__(self):
        super().__init__()

        # Window setup
        self.setWindowTitle("WindowSlu")
        self.setMinimumSize(400, 500)

        # Remove default window frame
        self.setWindowFlags(
            Qt.WindowType.FramelessWindowHint |
            Qt.WindowType.WindowStaysOnTopHint
        )

        # Main widget and layout
        self.central_widget = QWidget()
        self.setCentralWidget(self.central_widget)
        self.main_layout = QVBoxLayout(self.central_widget)

        # Create custom title bar
        self.create_title_bar()

        # Create tab widget
        self.create_tabs()

        # Create system tray icon
        self.create_tray_icon()

        # Track mouse for window dragging
        self.dragging = False
        self.drag_position = QPoint()

    def create_title_bar(self):
        title_bar = QWidget()
        title_bar.setFixedHeight(30)
        title_bar_layout = QHBoxLayout(title_bar)
        title_bar_layout.setContentsMargins(10, 0, 10, 0)

        # Title
        title_label = QLabel("WindowSlu")

        # Dark/Light mode toggle button
        self.theme_button = QPushButton()
        self.theme_button.setIcon(self.style().standardIcon(QStyle.StandardPixmap.SP_ComputerIcon))  # Placeholder icon
        self.theme_button.setFixedSize(24, 24)
        self.theme_button.clicked.connect(self.toggle_theme)

        # Minimize button
        minimize_button = QPushButton()
        minimize_button.setIcon(self.style().standardIcon(QStyle.StandardPixmap.SP_TitleBarMinButton))
        minimize_button.setFixedSize(24, 24)
        minimize_button.clicked.connect(self.showMinimized)

        # Close button
        close_button = QPushButton()
        close_button.setIcon(self.style().standardIcon(QStyle.StandardPixmap.SP_TitleBarCloseButton))
        close_button.setFixedSize(24, 24)
        close_button.clicked.connect(self.close)

        # Add widgets to title bar
        title_bar_layout.addWidget(title_label)
        title_bar_layout.addStretch()
        title_bar_layout.addWidget(self.theme_button)
        title_bar_layout.addWidget(minimize_button)
        title_bar_layout.addWidget(close_button)

        self.main_layout.addWidget(title_bar)

    def create_tabs(self):
        self.tab_widget = QTabWidget()

        # Main tab
        self.main_tab = QWidget()
        main_tab_layout = QVBoxLayout(self.main_tab)

        # Transparency slider for active window
        transparency_group = QWidget()
        transparency_layout = QVBoxLayout(transparency_group)

        transparency_label = QLabel("Active Window Transparency")
        self.transparency_slider = QSlider(Qt.Orientation.Horizontal)
        self.transparency_slider.setRange(0, 255)
        self.transparency_slider.setValue(255)  # Fully opaque
        self.transparency_slider.valueChanged.connect(self.change_active_window_transparency)

        transparency_layout.addWidget(transparency_label)
        transparency_layout.addWidget(self.transparency_slider)

        # Process list
        process_label = QLabel("Active Windows")
        self.process_list = QListWidget()
        self.refresh_button = QPushButton("Refresh Window List")
        self.refresh_button.clicked.connect(self.refresh_window_list)

        main_tab_layout.addWidget(transparency_group)
        main_tab_layout.addWidget(process_label)
        main_tab_layout.addWidget(self.process_list)
        main_tab_layout.addWidget(self.refresh_button)

        # Settings tab (placeholder for now)
        self.settings_tab = QWidget()
        settings_layout = QVBoxLayout(self.settings_tab)
        settings_layout.addWidget(QLabel("Settings will be available here"))

        # Add tabs to tab widget
        self.tab_widget.addTab(self.main_tab, "Main")
        self.tab_widget.addTab(self.settings_tab, "Settings")

        self.main_layout.addWidget(self.tab_widget)

    def create_tray_icon(self):
        self.tray_icon = QSystemTrayIcon(self)
        self.tray_icon.setIcon(self.style().standardIcon(QStyle.StandardPixmap.SP_ComputerIcon))  # Placeholder icon

        # Create tray menu
        tray_menu = QMenu()

        show_action = QAction("Show", self)
        show_action.triggered.connect(self.show)

        hide_action = QAction("Hide", self)
        hide_action.triggered.connect(self.hide)

        quit_action = QAction("Exit", self)
        quit_action.triggered.connect(self.close)

        tray_menu.addAction(show_action)
        tray_menu.addAction(hide_action)
        tray_menu.addSeparator()
        tray_menu.addAction(quit_action)

        self.tray_icon.setContextMenu(tray_menu)
        self.tray_icon.show()

    def toggle_theme(self):
        # Placeholder for theme toggle functionality
        print("Theme toggle clicked")

    def refresh_window_list(self):
        self.process_list.clear()

        def enum_windows_callback(hwnd, windows):
            if win32gui.IsWindowVisible(hwnd) and win32gui.GetWindowText(hwnd):
                windows.append((hwnd, win32gui.GetWindowText(hwnd)))
            return True

        windows = []
        win32gui.EnumWindows(enum_windows_callback, windows)

        for hwnd, title in windows:
            if title and title != self.windowTitle():  # Skip our own window
                item = QListWidgetItem(f"{title}")
                item.setData(Qt.ItemDataRole.UserRole, hwnd)  # Store window handle
                self.process_list.addItem(item)

        self.process_list.itemClicked.connect(self.on_window_selected)

    def on_window_selected(self, item):
        hwnd = item.data(Qt.ItemDataRole.UserRole)
        self.selected_window_hwnd = hwnd

        # Update slider to current transparency value (if we can get it)
        # For now, just reset to fully opaque
        self.transparency_slider.setValue(255)

    def change_active_window_transparency(self, value):
        if hasattr(self, 'selected_window_hwnd'):
            hwnd = self.selected_window_hwnd

            # Get current window style
            style = win32gui.GetWindowLong(hwnd, win32con.GWL_EXSTYLE)

            # Add layered window style if not already present
            if not (style & win32con.WS_EX_LAYERED):
                win32gui.SetWindowLong(
                    hwnd,
                    win32con.GWL_EXSTYLE,
                    style | win32con.WS_EX_LAYERED
                )

            # Set transparency level
            win32gui.SetLayeredWindowAttributes(
                hwnd,
                0,
                value,  # Alpha value (0-255)
                win32con.LWA_ALPHA
            )

    # Window dragging functionality
    def mousePressEvent(self, event):
        if event.button() == Qt.MouseButton.LeftButton:
            self.dragging = True
            self.drag_position = event.globalPosition().toPoint() - self.frameGeometry().topLeft()
            event.accept()

    def mouseReleaseEvent(self, event):
        if event.button() == Qt.MouseButton.LeftButton:
            self.dragging = False
            event.accept()

    def mouseMoveEvent(self, event):
        if self.dragging and event.buttons() & Qt.MouseButton.LeftButton:
            self.move(event.globalPosition().toPoint() - self.drag_position)
            event.accept()

if __name__ == '__main__':
    app = QApplication(sys.argv)
    window = WindowSlu()
    window.show()
    sys.exit(app.exec())