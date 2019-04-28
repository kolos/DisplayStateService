using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace DisplayStateService
{
    public partial class DisplayStateService : ServiceBase
    {
        [DllImport("User32", SetLastError = true, EntryPoint = "RegisterPowerSettingNotification", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, Int32 Flags);

        [DllImport("User32", EntryPoint = "UnregisterPowerSettingNotification", CallingConvention = CallingConvention.StdCall)]
        private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        private delegate IntPtr ServiceControlHandlerEx(int control, int eventType, IntPtr eventData, IntPtr context);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern IntPtr RegisterServiceCtrlHandlerEx(string lpServiceName, ServiceControlHandlerEx cbex, IntPtr context);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct POWERBROADCAST_SETTING
        {
            public Guid PowerSetting;
            public uint DataLength;
            public byte Data;
        }

        private static Guid GUID_CONSOLE_DISPLAY_STATE = new Guid(0x6fe69556, 0x704a, 0x47a0, 0x8f, 0x24, 0xc2, 0x8d, 0x93, 0x6f, 0xda, 0x47);
        private const int DEVICE_NOTIFY_SERVICE_HANDLE = 0x00000001;
        private const int SERVICE_CONTROL_POWEREVENT = 0x0000000D;
        private const int PBT_POWERSETTINGCHANGE = 0x8013;

        IntPtr powerSettingsNotificationHandle;

        bool prevDisplayState = true;

        public bool RegisterLidEventNotifications(IntPtr serviceHandle, string serviceName)
        {
            powerSettingsNotificationHandle = RegisterPowerSettingNotification(serviceHandle,
                ref GUID_CONSOLE_DISPLAY_STATE,
                DEVICE_NOTIFY_SERVICE_HANDLE);

            var serviceCtrlHandler = RegisterServiceCtrlHandlerEx(serviceName, MessageHandler, IntPtr.Zero);

            if (serviceCtrlHandler == IntPtr.Zero)
                return false;

            return powerSettingsNotificationHandle != IntPtr.Zero;
        }

        public bool UnregisterLidEventNotifications()
        {
            return powerSettingsNotificationHandle != IntPtr.Zero && UnregisterPowerSettingNotification(powerSettingsNotificationHandle);
        }

        private IntPtr MessageHandler(int dwControl, int dwEventType, IntPtr lpEventData, IntPtr lpContext)
        {
            // If dwControl is SERVICE_CONTROL_POWEREVENT
            // and dwEventType is PBT_POWERSETTINGCHANGE
            // then lpEventData is a pointer to a POWERBROADCAST_SETTING struct
            // Ref. https://msdn.microsoft.com/en-us/library/ms683241(v=vs.85).aspx
            if (dwControl == SERVICE_CONTROL_POWEREVENT && dwEventType == PBT_POWERSETTINGCHANGE)
            {
                var ps = (POWERBROADCAST_SETTING)Marshal.PtrToStructure(lpEventData, typeof(POWERBROADCAST_SETTING));

                if (ps.PowerSetting == GUID_CONSOLE_DISPLAY_STATE)
                {
                    var isDisplayStateOn = ps.Data != 0;

                    if(prevDisplayState != isDisplayStateOn)
                    {
                        string commandToRun = @"C:\Program Files\ProgramTo\Run.exe";
                        string commandArguments = "arg1 arg2 arg..";

                        Process.Start(commandToRun, commandArguments);
                    }

                    prevDisplayState = isDisplayStateOn;
                }
            }

            return IntPtr.Zero;
        }


        public DisplayStateService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            RegisterLidEventNotifications(ServiceHandle, ServiceName);
        }

        protected override void OnStop()
        {
            UnregisterLidEventNotifications();
        }
    }
}
