using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using UnityEngine;

namespace TurnOnTheBass
{
    public sealed class PicoSerialRhythmInput : MonoBehaviour
    {
        [Header("Rhythm Target")]
        [SerializeField] private CanvasRhythmGame rhythmGame;
        [SerializeField] private bool autoFindRhythmGame = true;

        [Header("Serial Port")]
        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private string portName = "COM3";
        [SerializeField] private int baudRate = 9600;
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
        [SerializeField, Min(0f)] private float sameButtonIgnoreSeconds = 0.08f;

        private readonly ConcurrentQueue<string> pendingLines = new ConcurrentQueue<string>();
        private readonly float[] lastButtonPressTimes = { -999f, -999f, -999f, -999f };
        private SafeFileHandle serialHandle;
        private Thread readThread;
        private volatile bool keepReading;

        public bool IsConnected => serialHandle != null && !serialHandle.IsInvalid && !serialHandle.IsClosed;

        private void Awake()
        {
            if (autoFindRhythmGame && rhythmGame == null)
            {
                rhythmGame = FindRhythmGameIncludingInactive();
            }
        }

        private void Start()
        {
            if (connectOnStart)
            {
                Connect();
            }
        }

        private void Update()
        {
            while (pendingLines.TryDequeue(out string line))
            {
                ProcessLine(line);
            }
        }

        private void OnDisable()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public void Connect()
        {
            if (IsConnected)
            {
                return;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                serialHandle = NativeSerial.Open(portName, baudRate, readIntervalTimeoutMs, dtrEnable, rtsEnable);
                keepReading = true;
                readThread = new Thread(ReadSerialLoop)
                {
                    IsBackground = true,
                    Name = "Pico Serial Rhythm Input"
                };
                readThread.Start();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not open Pico serial port " + portName + ": " + exception.Message, this);
                CleanupSerial();
            }
#else
            Debug.LogWarning("Pico serial input is currently implemented for Windows editor/player builds.", this);
#endif
        }

        public void Disconnect()
        {
            keepReading = false;

            if (readThread != null && readThread.IsAlive)
            {
                readThread.Join(150);
            }

            CleanupSerial();
        }

        private void ReadSerialLoop()
        {
            byte[] buffer = new byte[128];
            StringBuilder lineBuilder = new StringBuilder(128);

            while (keepReading && IsConnected)
            {
                try
                {
                    int bytesRead = NativeSerial.Read(serialHandle, buffer);
                    for (int index = 0; index < bytesRead; index++)
                    {
                        char character = (char)buffer[index];
                        if (character == '\n')
                        {
                            EnqueueLine(lineBuilder);
                            continue;
                        }

                        if (character != '\r')
                        {
                            lineBuilder.Append(character);
                        }
                    }
                }
                catch (Exception exception)
                {
                    pendingLines.Enqueue("SERIAL_ERROR:" + exception.Message);
                    keepReading = false;
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
                HandleButtonPress(0);
                return;
            }

            if (line.Equals(button2Message, StringComparison.OrdinalIgnoreCase))
            {
                HandleButtonPress(1);
                return;
            }

            if (line.Equals(button3Message, StringComparison.OrdinalIgnoreCase))
            {
                HandleButtonPress(2);
                return;
            }

            if (line.Equals(button4Message, StringComparison.OrdinalIgnoreCase))
            {
                HandleButtonPress(3);
            }
        }

        private void HandleButtonPress(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= lastButtonPressTimes.Length)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - lastButtonPressTimes[laneIndex] < sameButtonIgnoreSeconds)
            {
                return;
            }

            lastButtonPressTimes[laneIndex] = now;
            rhythmGame.HandleLanePressed(laneIndex);
        }

        private void CleanupSerial()
        {
            if (serialHandle != null)
            {
                if (!serialHandle.IsClosed)
                {
                    serialHandle.Close();
                }

                serialHandle.Dispose();
                serialHandle = null;
            }

            readThread = null;
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

        private static class NativeSerial
        {
            private const uint GenericRead = 0x80000000;
            private const uint OpenExisting = 3;
            private const int InvalidHandleValue = -1;

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
                    ReadIntervalTimeout = (uint)Mathf.Max(1, readIntervalTimeoutMs),
                    ReadTotalTimeoutMultiplier = 0,
                    ReadTotalTimeoutConstant = (uint)Mathf.Max(1, readIntervalTimeoutMs),
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
