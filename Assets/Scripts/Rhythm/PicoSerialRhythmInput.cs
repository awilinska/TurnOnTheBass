using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using UnityEngine;
using TurnOnTheBass.UI;

namespace TurnOnTheBass
{
    public sealed class PicoSerialRhythmInput : MonoBehaviour
    {
        [Header("Rhythm Target")]
        [SerializeField] private CanvasRhythmGame rhythmGame;
        [SerializeField] private bool autoFindRhythmGame = true;

        [Header("UI Navigation")]
        [SerializeField] private bool enableUiNavigation = true;
        [SerializeField] private bool preferRhythmInputWhilePlaying = true;
        [SerializeField] private UiScreenSwitcher screenSwitcher;
        [SerializeField] private GameObject finishedScreen;
        [SerializeField] private GameObject menuScreen;
        [SerializeField] private bool button4ReturnsFromFinishedToMenu = true;

        [Header("Serial Port")]
        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private bool autoDetectPort = true;
        [Tooltip("COM port to use when Auto Detect Port is off.")]
        [SerializeField] private string portName = "COM3";
        [SerializeField] private int baudRate = 9600;
        [Tooltip("Maximum time to wait for bytes when the serial input buffer is empty.")]
        [SerializeField, Min(1)] private int readIntervalTimeoutMs = 50;
        [SerializeField] private bool dtrEnable = true;
        [SerializeField] private bool rtsEnable = true;
        [SerializeField] private bool logSerialLines;

        [Header("Message Mapping")]
        [SerializeField] private string spinPrefix = "OBROT:";
        [SerializeField] private string button1Message = "BUTTON_1 pressed";
        [SerializeField] private string button2Message = "BUTTON_2 pressed";
        [SerializeField] private string button3Message = "BUTTON_3 pressed";
        [SerializeField] private string button4Message = "BUTTON_4 pressed";

        [Header("Input Debounce")]
        [Tooltip("Minimum time in seconds between accepted presses of the same controller button.")]
        [SerializeField, Min(0f)] private float sameButtonIgnoreSeconds = 0.15f;

        private const float ConnectionRetrySeconds = 1f;

        private readonly ConcurrentQueue<string> pendingLines = new ConcurrentQueue<string>();
        private readonly float[] lastButtonPressTimes = { -999f, -999f, -999f, -999f };
        private Thread readThread;
        private volatile bool stopReading;
        private volatile string connectedPort;
        private string reportedPort;
        private bool connectionRequested;
        private float nextConnectionAttemptTime;

        public bool IsConnected => !stopReading && connectedPort != null;

        private void Awake()
        {
            if (autoFindRhythmGame && rhythmGame == null)
            {
                rhythmGame = FindRhythmGameIncludingInactive();
            }

            ResolveUiReferencesIfMissing();
        }

        private void OnEnable()
        {
            if (connectOnStart)
            {
                Connect();
            }
        }

        private void Update()
        {
            string activePort = stopReading ? null : connectedPort;
            if (activePort != reportedPort)
            {
                reportedPort = activePort;
                if (activePort != null)
                {
                    Debug.Log("Pico controller connected on " + activePort + ".", this);
                }
            }

            while (pendingLines.TryDequeue(out string line))
            {
                if (!stopReading)
                {
                    ProcessLine(line);
                }
            }

            StartSerialReaderIfNeeded();
        }

        private void OnDisable()
        {
            Disconnect();
        }

        public void Connect()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!stopReading && readThread != null && readThread.IsAlive)
            {
                return;
            }

            connectionRequested = true;
            nextConnectionAttemptTime = 0f;
            StartSerialReaderIfNeeded();
#else
            Debug.LogWarning("Pico serial input is currently implemented for Windows editor/player builds.", this);
#endif
        }

        public void Disconnect()
        {
            connectionRequested = false;
            stopReading = true;
            reportedPort = null;

            if (readThread != null && readThread.IsAlive)
            {
                readThread.Join(150);
            }
        }

        private void StartSerialReaderIfNeeded()
        {
            if (!connectionRequested || Time.unscaledTime < nextConnectionAttemptTime ||
                (readThread != null && readThread.IsAlive))
            {
                return;
            }

            if (stopReading)
            {
                while (pendingLines.TryDequeue(out _))
                {
                }
            }

            bool detectPort = autoDetectPort;
            string manualPort = string.IsNullOrWhiteSpace(portName) ? "COM3" : portName.Trim();
            connectionRequested = detectPort;
            nextConnectionAttemptTime = Time.unscaledTime + ConnectionRetrySeconds;
            stopReading = false;
            readThread = new Thread(() => ReadSerial(detectPort, manualPort))
            {
                IsBackground = true,
                Name = "Pico Serial Rhythm Input"
            };
            readThread.Start();
        }

        private void ReadSerial(bool detectPort, string manualPort)
        {
            try
            {
                if (!detectPort)
                {
                    TryReadPort(manualPort, false);
                    return;
                }

                foreach (string detectedPort in NativeSerial.FindPicoPorts())
                {
                    if (stopReading || TryReadPort(detectedPort, true))
                    {
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                if (!stopReading)
                {
                    pendingLines.Enqueue("SERIAL_ERROR:" + exception.Message);
                }
            }
        }

        private bool TryReadPort(string port, bool detectPort)
        {
            SafeFileHandle handle;
            try
            {
                handle = NativeSerial.Open(port, baudRate, readIntervalTimeoutMs, dtrEnable, rtsEnable);
            }
            catch (InvalidOperationException) when (detectPort)
            {
                // A device can disappear or be opened by another application during discovery.
                return false;
            }

            using (handle)
            {
                if (stopReading)
                {
                    return true;
                }

                connectedPort = port;
                try
                {
                    ReadSerialLines(handle);
                }
                catch (InvalidOperationException) when (detectPort)
                {
                    // Let the next discovery attempt pick up the reconnected device.
                }
                finally
                {
                    connectedPort = null;
                }
            }

            return true;
        }

        private void ReadSerialLines(SafeFileHandle handle)
        {
            byte[] buffer = new byte[128];
            StringBuilder lineBuilder = new StringBuilder(128);

            while (!stopReading)
            {
                int bytesRead = NativeSerial.Read(handle, buffer);
                for (int index = 0; index < bytesRead && !stopReading; index++)
                {
                    char character = (char)buffer[index];
                    if (character == '\n')
                    {
                        EnqueueLine(lineBuilder);
                    }
                    else if (character != '\r')
                    {
                        lineBuilder.Append(character);
                    }
                }
            }
        }

        private void EnqueueLine(StringBuilder lineBuilder)
        {
            string line = lineBuilder.ToString().Trim();
            lineBuilder.Length = 0;

            if (!string.IsNullOrWhiteSpace(line))
            {
                pendingLines.Enqueue(line);
            }
        }

        private void ProcessLine(string line)
        {
            if (logSerialLines)
            {
                Debug.Log("Pico serial: " + line, this);
            }

            if (line.StartsWith("SERIAL_ERROR:", StringComparison.Ordinal))
            {
                Debug.LogWarning(line, this);
                return;
            }

            if (rhythmGame == null && autoFindRhythmGame)
            {
                rhythmGame = FindRhythmGameIncludingInactive();
            }

            if (rhythmGame == null)
            {
                return;
            }

            if (line.StartsWith(spinPrefix, StringComparison.OrdinalIgnoreCase))
            {
                rhythmGame.HandleSpinTick();
                return;
            }

            if (line.Equals(button1Message, StringComparison.OrdinalIgnoreCase))
            {
                HandleControllerButton(0);
                return;
            }

            if (line.Equals(button2Message, StringComparison.OrdinalIgnoreCase))
            {
                HandleControllerButton(1);
                return;
            }

            if (line.Equals(button3Message, StringComparison.OrdinalIgnoreCase))
            {
                HandleControllerButton(2);
                return;
            }

            if (line.Equals(button4Message, StringComparison.OrdinalIgnoreCase))
            {
                HandleControllerButton(3);
            }
        }

        private void HandleControllerButton(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= lastButtonPressTimes.Length)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - lastButtonPressTimes[buttonIndex] < sameButtonIgnoreSeconds)
            {
                return;
            }

            lastButtonPressTimes[buttonIndex] = now;

            if (TryHandleUiButton(buttonIndex))
            {
                return;
            }

            HandleRhythmButton(buttonIndex);
        }

        private bool TryHandleUiButton(int buttonIndex)
        {
            if (!enableUiNavigation || ShouldUseRhythmInput())
            {
                return false;
            }

            if (IsFinishedScreenActive())
            {
                if (button4ReturnsFromFinishedToMenu && buttonIndex == 3 &&
                    (!finishedScreen.TryGetComponent(out RhythmResultsScreen results) || results.CanDismiss))
                {
                    ShowMenuScreen();
                }

                return true;
            }

            switch (buttonIndex)
            {
                case 0:
                    return TrySubmitActiveUi();
                case 2:
                    return TryMoveActiveUi(-1);
                case 3:
                    return TryMoveActiveUi(1);
                default:
                    return false;
            }
        }

        private bool ShouldUseRhythmInput()
        {
            return preferRhythmInputWhilePlaying &&
                   rhythmGame != null &&
                   rhythmGame.gameObject.activeInHierarchy &&
                   rhythmGame.IsPlaying;
        }

        private bool TryMoveActiveUi(int direction)
        {
            SongCarouselSelector carousel = FindActiveSongCarousel();
            if (carousel != null)
            {
                carousel.Move(direction);
                return true;
            }

            ControllerMenuNavigator menu = FindActiveMenuNavigator();
            if (menu != null)
            {
                menu.Move(direction);
                return true;
            }

            return false;
        }

        private bool IsFinishedScreenActive()
        {
            ResolveUiReferencesIfMissing();
            return finishedScreen != null && finishedScreen.activeInHierarchy &&
                   (screenSwitcher == null || screenSwitcher.CurrentScreen == finishedScreen);
        }

        private void ShowMenuScreen()
        {
            ResolveUiReferencesIfMissing();

            if (screenSwitcher != null && menuScreen != null)
            {
                screenSwitcher.Show(menuScreen);
                return;
            }

            if (menuScreen != null)
            {
                menuScreen.SetActive(true);
            }

            if (finishedScreen != null)
            {
                finishedScreen.SetActive(false);
            }
        }

        private void ResolveUiReferencesIfMissing()
        {
            if (screenSwitcher == null)
            {
                screenSwitcher = FindSceneObjectIncludingInactive<UiScreenSwitcher>();
            }

            if (finishedScreen == null)
            {
                finishedScreen = FindSceneGameObjectByName("Finished");
            }

            if (menuScreen == null)
            {
                menuScreen = FindSceneGameObjectByName("Menu");
            }
        }

        private bool TrySubmitActiveUi()
        {
            SongCarouselSelector carousel = FindActiveSongCarousel();
            if (carousel != null)
            {
                carousel.ConfirmSelection();
                return true;
            }

            ControllerMenuNavigator menu = FindActiveMenuNavigator();
            if (menu != null)
            {
                menu.ActivateSelected();
                return true;
            }

            return false;
        }

        private void HandleRhythmButton(int laneIndex)
        {
            if (rhythmGame == null)
            {
                return;
            }

            rhythmGame.HandleLanePressed(laneIndex);
        }

        private static CanvasRhythmGame FindRhythmGameIncludingInactive()
        {
            CanvasRhythmGame[] rhythmGames = Resources.FindObjectsOfTypeAll<CanvasRhythmGame>();
            for (int index = 0; index < rhythmGames.Length; index++)
            {
                CanvasRhythmGame game = rhythmGames[index];
                if (game != null && game.gameObject.scene.IsValid())
                {
                    return game;
                }
            }

            return null;
        }

        private static SongCarouselSelector FindActiveSongCarousel()
        {
            SongCarouselSelector[] carousels = Resources.FindObjectsOfTypeAll<SongCarouselSelector>();
            for (int index = 0; index < carousels.Length; index++)
            {
                SongCarouselSelector carousel = carousels[index];
                if (carousel != null && carousel.gameObject.scene.IsValid() && carousel.isActiveAndEnabled)
                {
                    return carousel;
                }
            }

            return null;
        }

        private static ControllerMenuNavigator FindActiveMenuNavigator()
        {
            ControllerMenuNavigator[] menus = Resources.FindObjectsOfTypeAll<ControllerMenuNavigator>();
            for (int index = 0; index < menus.Length; index++)
            {
                ControllerMenuNavigator menu = menus[index];
                if (menu != null && menu.gameObject.scene.IsValid() && menu.isActiveAndEnabled)
                {
                    return menu;
                }
            }

            return null;
        }

        private static T FindSceneObjectIncludingInactive<T>() where T : UnityEngine.Object
        {
            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            for (int index = 0; index < objects.Length; index++)
            {
                T candidate = objects[index];
                if (candidate is Component component && component.gameObject.scene.IsValid())
                {
                    return candidate;
                }
            }

            return null;
        }

        private static GameObject FindSceneGameObjectByName(string objectName)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int index = 0; index < objects.Length; index++)
            {
                GameObject candidate = objects[index];
                if (candidate != null && candidate.scene.IsValid() && candidate.name == objectName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static class NativeSerial
        {
            private const uint GenericRead = 0x80000000;
            private const uint OpenExisting = 3;
            private const uint PresentUsbDevices = 0x00000101; // CM_GETIDLIST_FILTER_PRESENT | CM_GETIDLIST_FILTER_ENUMERATOR
            private const uint KeyQueryValue = 0x0001;
            private const uint RegistryOpenExisting = 0x0001;
            private const uint RegistryHardware = 0;
            private const uint RegistryString = 0x00000002; // RRF_RT_REG_SZ
            private const string PicoUsbIdPrefix = @"USB\VID_2E8A&PID_";

            [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string filter, uint flags);

            [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern uint CM_Get_Device_ID_ListW(string filter, [Out] char[] buffer, uint length, uint flags);

            [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern uint CM_Locate_DevNodeW(out uint device, string deviceId, uint flags);

            [DllImport("cfgmgr32.dll")]
            private static extern uint CM_Open_DevNode_Key(uint device, uint access, uint profile, uint disposition, out IntPtr key, uint flags);

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern int RegGetValueW(IntPtr key, string subKey, string name, uint flags, IntPtr type, StringBuilder value, ref uint size);

            [DllImport("advapi32.dll")]
            private static extern int RegCloseKey(IntPtr key);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern SafeFileHandle CreateFile(
                string fileName,
                uint desiredAccess,
                uint shareMode,
                IntPtr securityAttributes,
                uint creationDisposition,
                uint flagsAndAttributes,
                IntPtr templateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool ReadFile(
                SafeFileHandle file,
                byte[] buffer,
                uint numberOfBytesToRead,
                out uint numberOfBytesRead,
                IntPtr overlapped);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
            private static extern bool BuildCommDCB(string definition, ref Dcb dcb);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool SetCommState(SafeFileHandle file, ref Dcb dcb);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool SetCommTimeouts(SafeFileHandle file, ref CommTimeouts timeouts);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool EscapeCommFunction(SafeFileHandle file, uint function);

            public static IEnumerable<string> FindPicoPorts()
            {
                const string usbEnumerator = "USB";
                if (CM_Get_Device_ID_List_SizeW(out uint length, usbEnumerator, PresentUsbDevices) != 0 || length <= 1)
                {
                    yield break;
                }

                char[] buffer = new char[length];
                if (CM_Get_Device_ID_ListW(usbEnumerator, buffer, length, PresentUsbDevices) != 0)
                {
                    yield break;
                }

                foreach (string deviceId in new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!deviceId.StartsWith(PicoUsbIdPrefix, StringComparison.OrdinalIgnoreCase) ||
                        CM_Locate_DevNodeW(out uint device, deviceId, 0) != 0 ||
                        CM_Open_DevNode_Key(device, KeyQueryValue, 0, RegistryOpenExisting, out IntPtr key, RegistryHardware) != 0)
                    {
                        continue;
                    }

                    string port = null;
                    try
                    {
                        StringBuilder value = new StringBuilder(32);
                        uint size = (uint)(value.Capacity * sizeof(char));
                        if (RegGetValueW(key, null, "PortName", RegistryString, IntPtr.Zero, value, ref size) == 0)
                        {
                            port = value.ToString();
                        }
                    }
                    finally
                    {
                        RegCloseKey(key);
                    }

                    if (port != null && port.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return port;
                    }
                }
            }

            public static SafeFileHandle Open(string portName, int baudRate, int readIntervalTimeoutMs, bool dtrEnable, bool rtsEnable)
            {
                string normalizedPortName = NormalizePortName(portName);
                SafeFileHandle handle = CreateFile(
                    normalizedPortName,
                    GenericRead,
                    0,
                    IntPtr.Zero,
                    OpenExisting,
                    0,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    throw new InvalidOperationException("CreateFile failed for " + normalizedPortName + ". Win32 error: " + Marshal.GetLastWin32Error());
                }

                Dcb dcb = new Dcb
                {
                    DCBlength = (uint)Marshal.SizeOf(typeof(Dcb))
                };

                string dcbDefinition = "baud=" + baudRate + " parity=N data=8 stop=1";
                if (!BuildCommDCB(dcbDefinition, ref dcb))
                {
                    handle.Dispose();
                    throw new InvalidOperationException("BuildCommDCB failed. Win32 error: " + Marshal.GetLastWin32Error());
                }

                if (!SetCommState(handle, ref dcb))
                {
                    handle.Dispose();
                    throw new InvalidOperationException("SetCommState failed. Win32 error: " + Marshal.GetLastWin32Error());
                }

                CommTimeouts timeouts = new CommTimeouts
                {
                    // Return available controller input immediately and wait only when the buffer is empty.
                    ReadIntervalTimeout = uint.MaxValue,
                    ReadTotalTimeoutMultiplier = uint.MaxValue,
                    ReadTotalTimeoutConstant = (uint)Math.Max(1, readIntervalTimeoutMs),
                    WriteTotalTimeoutMultiplier = 0,
                    WriteTotalTimeoutConstant = 0
                };

                if (!SetCommTimeouts(handle, ref timeouts))
                {
                    handle.Dispose();
                    throw new InvalidOperationException("SetCommTimeouts failed. Win32 error: " + Marshal.GetLastWin32Error());
                }

                EscapeCommFunction(handle, dtrEnable ? 5u : 6u);
                EscapeCommFunction(handle, rtsEnable ? 3u : 4u);

                return handle;
            }

            public static int Read(SafeFileHandle handle, byte[] buffer)
            {
                if (!ReadFile(handle, buffer, (uint)buffer.Length, out uint bytesRead, IntPtr.Zero))
                {
                    throw new InvalidOperationException("ReadFile failed. Win32 error: " + Marshal.GetLastWin32Error());
                }

                return (int)bytesRead;
            }

            private static string NormalizePortName(string value)
            {
                string trimmed = string.IsNullOrWhiteSpace(value) ? "COM3" : value.Trim();
                return trimmed.StartsWith(@"\\.\", StringComparison.Ordinal) ? trimmed : @"\\.\" + trimmed;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct CommTimeouts
            {
                public uint ReadIntervalTimeout;
                public uint ReadTotalTimeoutMultiplier;
                public uint ReadTotalTimeoutConstant;
                public uint WriteTotalTimeoutMultiplier;
                public uint WriteTotalTimeoutConstant;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct Dcb
            {
                public uint DCBlength;
                public uint BaudRate;
                public uint Flags;
                public ushort WReserved;
                public ushort XonLim;
                public ushort XoffLim;
                public byte ByteSize;
                public byte Parity;
                public byte StopBits;
                public sbyte XonChar;
                public sbyte XoffChar;
                public sbyte ErrorChar;
                public sbyte EofChar;
                public sbyte EvtChar;
                public ushort WReserved1;
            }
        }
    }
}
