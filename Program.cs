using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoClicker
{
    public class MainForm : Form
    {
        private Button startButton;  // Button to start clicking
        private Button stopButton;   // Button to stop clicking
        private Label statusLabel;   // Label to display the current status (Started/Stopped)
        private Random random = new Random();  // Random generator to vary the click rate
        private CancellationTokenSource cts;  // Token to cancel the clicking task
        private LowLevelKeyboardHook keyboardHook; // Hook for detecting global keyboard input

        // Import Windows API for mouse events
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        // Constants for mouse events
        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;

        public MainForm()
        {
            // Set up the form
            Text = "Auto Clicker";  // Title of the window
            Size = new Size(300, 200);  // Set size of the window
            FormBorderStyle = FormBorderStyle.FixedDialog;  // Make the window non-resizable
            MaximizeBox = false;  // Disable maximize button

            // Initialize and configure the start button
            startButton = new Button
            {
                Text = "Start Clicking",  // Text on the button
                Location = new Point(50, 50)  // Position of the button
            };
            startButton.Click += StartClicking;  // Event handler for button click
            Controls.Add(startButton);  // Add button to form

            // Initialize and configure the stop button
            stopButton = new Button
            {
                Text = "Stop Clicking",  // Text on the button
                Location = new Point(150, 50)  // Position of the button
            };
            stopButton.Click += StopClicking;  // Event handler for button click
            Controls.Add(stopButton);  // Add button to form

            // Initialize and configure the status label
            statusLabel = new Label
            {
                Text = "Status: Stopped",  // Initial text
                AutoSize = true,  // Label adjusts to text size
                Location = new Point(50, 100)  // Position of the label
            };
            Controls.Add(statusLabel);  // Add label to form

            // Set up global keyboard hook to listen for keypress events
            keyboardHook = new LowLevelKeyboardHook();
            keyboardHook.OnKeyPressed += OnKeyPress;  // Event handler for key press
            keyboardHook.HookKeyboard();  // Start listening for global keyboard events
        }

        private void OnKeyPress(Keys key)
        {
            // If the '1' key is pressed, start the clicking
            if (key == Keys.D1) StartClicking(null, EventArgs.Empty); 
            // If the '2' key is pressed, stop the clicking
            if (key == Keys.D2) StopClicking(null, EventArgs.Empty);  
        }

        // Starts the clicking task when the start button is clicked or global hotkey '1' is pressed
        private void StartClicking(object sender, EventArgs e)
        {
            if (cts != null) return; // Prevent multiple clicking tasks running simultaneously
            cts = new CancellationTokenSource();  // Create a new cancellation token
            statusLabel.Text = "Status: Clicking...";  // Update status
            Task.Run(() => ClickLoop(cts.Token), cts.Token);  // Run clicking loop in a background task
        }

        // Stops the clicking task when the stop button is clicked or global hotkey '2' is pressed
        private void StopClicking(object sender, EventArgs e)
        {
            if (cts == null) return;  // No clicking task is running
            cts.Cancel();  // Cancel the task
            cts = null;  // Reset the cancellation token source
            statusLabel.Text = "Status: Stopped";  // Update status
        }

        // The main loop that simulates the mouse clicking
        private async Task ClickLoop(CancellationToken token)
        {
            // Keep clicking until the task is canceled
            while (!token.IsCancellationRequested)
            {
                DoClick();  // Simulate a click
                int delay = 60000 / random.Next(260, 280);  // Random interval between clicks (between 100-300 clicks per minute)
                for (int i = 0; i < delay / 10; i++)  // Check every 10ms if stop is requested
                {
                    if (token.IsCancellationRequested) return;
                    await Task.Delay(10);  // Wait for 10ms
                }
            }
        }

        // Simulates a mouse click at the current cursor position
        private void DoClick()
        {
            Point position = Cursor.Position;  // Get current mouse position (supports multiple screens)
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);  // Simulate mouse button down
            Thread.Sleep(random.Next(10, 50));  // Random delay to simulate natural clicking speed
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);  // Simulate mouse button up
        }

        // Clean up when the form is closed
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            keyboardHook.UnhookKeyboard();  // Unhook the global keyboard listener
            base.OnFormClosing(e);  // Call the base class method
        }

        // Entry point of the application
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();  // Enable visual styles for the application
            Application.SetCompatibleTextRenderingDefault(false);  // Set text rendering default
            Application.Run(new MainForm());  // Start the application with the main form
        }
    }

    // Class for handling global keyboard hooks
    public class LowLevelKeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;  // Low-level keyboard hook ID
        private const int WM_KEYDOWN = 0x0100;  // Key down event
        private IntPtr hookId = IntPtr.Zero;  // Hook ID for the keyboard hook

        public delegate void KeyPressedHandler(Keys key);  // Delegate for key press event
        public event KeyPressedHandler OnKeyPressed;  // Event triggered on key press

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);  // Low-level keyboard procedure
        private LowLevelKeyboardProc keyboardProc;  // The callback for the hook procedure

        [DllImport("user32.dll")]  // Import necessary Windows API functions
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // Hook the keyboard to listen for key presses
        public void HookKeyboard()
        {
            keyboardProc = HookCallback;  // Assign callback for the hook
            hookId = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProc, GetModuleHandle(null), 0);  // Set the hook
        }

        // Unhook the keyboard when no longer needed
        public void UnhookKeyboard()
        {
            if (hookId != IntPtr.Zero) UnhookWindowsHookEx(hookId);  // Unhook if hook is set
        }

        // Callback procedure for handling key press events
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)  // Check if key is pressed
            {
                int vkCode = Marshal.ReadInt32(lParam);  // Read the virtual key code
                OnKeyPressed?.Invoke((Keys)vkCode);  // Raise the key press event
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);  // Pass the event to the next hook
        }
    }
}
