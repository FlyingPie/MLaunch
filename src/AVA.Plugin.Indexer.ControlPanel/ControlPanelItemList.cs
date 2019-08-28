﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace AVA.Plugin.Indexer.ControlPanel
{
    public static class ControlPanelItemList
    {
        private const uint GROUP_ICON = 14;
        private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        private const string CONTROL = @"%SystemRoot%\System32\control.exe";

        private delegate bool EnumResNameDelegate(
        IntPtr hModule,
        IntPtr lpszType,
        IntPtr lpszName,
        IntPtr lParam);

        [DllImport("kernel32.dll", EntryPoint = "EnumResourceNamesW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumResourceNamesWithID(IntPtr hModule, uint lpszType, EnumResNameDelegate lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int LoadString(IntPtr hInstance, uint uID, StringBuilder lpBuffer, int nBufferMax);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hinst, IntPtr lpszName, uint uType,
        int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        private static IntPtr defaultIconPtr;

        private static RegistryKey nameSpace = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ControlPanel\\NameSpace");
        private static RegistryKey clsid = Registry.ClassesRoot.OpenSubKey("CLSID");

        public static List<ControlPanelItem> Create()
        {
            RegistryKey currentKey;
            ProcessStartInfo executablePath;
            List<ControlPanelItem> controlPanelItems = new List<ControlPanelItem>();
            string localizedString;
            string infoTip;
            Icon myIcon;

            foreach (string key in nameSpace.GetSubKeyNames())
            {
                currentKey = clsid.OpenSubKey(key);
                if (currentKey != null)
                {
                    executablePath = getExecutablePath(currentKey);

                    if (!(executablePath == null)) //Cannot have item without executable path
                    {
                        localizedString = getLocalizedString(currentKey);

                        if (!string.IsNullOrEmpty(localizedString))//Cannot have item without Title
                        {
                            infoTip = getInfoTip(currentKey);

                            controlPanelItems.Add(new ControlPanelItem(localizedString, infoTip, executablePath.ToPSI(), key));
                        }
                    }
                }
            }

            return controlPanelItems;
        }

        private static ProcessStartInfo getExecutablePath(RegistryKey currentKey)
        {
            ProcessStartInfo executablePath = new ProcessStartInfo();
            string applicationName;

            if (currentKey.GetValue("System.ApplicationName") != null)
            {
                //CPL Files (usually native MS items)
                applicationName = currentKey.GetValue("System.ApplicationName").ToString();
                executablePath.FileName = Environment.ExpandEnvironmentVariables(CONTROL);
                executablePath.Arguments = "-name " + applicationName;
            }
            else if (currentKey.OpenSubKey("Shell\\Open\\Command") != null && currentKey.OpenSubKey("Shell\\Open\\Command").GetValue(null) != null)
            {
                //Other files (usually third party items)
                string input = "\"" + Environment.ExpandEnvironmentVariables(currentKey.OpenSubKey("Shell\\Open\\Command").GetValue(null).ToString()) + "\"";
                executablePath.FileName = "cmd.exe";
                executablePath.Arguments = "/C " + input;
                executablePath.WindowStyle = ProcessWindowStyle.Hidden;
            }
            else
            {
                return null;
            }

            return executablePath;
        }

        private static string getLocalizedString(RegistryKey currentKey)
        {
            IntPtr dataFilePointer;
            string[] localizedStringRaw;
            uint stringTableIndex;
            StringBuilder resource;
            string localizedString;

            if (currentKey.GetValue("LocalizedString") != null)
            {
                if (currentKey.GetValue("LocalizedString").ToString()[0] == '@') //Uses string indexes
                {
                    localizedStringRaw = currentKey.GetValue("LocalizedString").ToString().Split(new string[] { ",-" }, StringSplitOptions.None);

                    localizedStringRaw[0] = localizedStringRaw[0].Substring(1);

                    localizedStringRaw[0] = Environment.ExpandEnvironmentVariables(localizedStringRaw[0]);

                    dataFilePointer = LoadLibraryEx(localizedStringRaw[0], IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE); //Load file with strings

                    if (localizedStringRaw.Length == 1) return currentKey.GetValue(null).ToString(); //return Path.GetFileNameWithoutExtension(localizedStringRaw[0]);

                    stringTableIndex = sanitizeUint(localizedStringRaw[1]);

                    resource = new StringBuilder(255);
                    LoadString(dataFilePointer, stringTableIndex, resource, resource.Capacity + 1); //Extract needed string
                    FreeLibrary(dataFilePointer);

                    localizedString = resource.ToString();

                    //Some apps don't return a string, although they do have a stringIndex. Use Default value.

                    if (String.IsNullOrEmpty(localizedString))
                    {
                        if (currentKey.GetValue(null) != null)
                        {
                            localizedString = currentKey.GetValue(null).ToString();
                        }
                        else
                        {
                            return null; //Cannot have item without title.
                        }
                    }
                }
                else
                {
                    localizedString = currentKey.GetValue("LocalizedString").ToString();
                }
            }
            else if (currentKey.GetValue(null) != null)
            {
                localizedString = currentKey.GetValue(null).ToString();
            }
            else
            {
                return null; //Cannot have item without title.
            }

            return localizedString;
        }

        private static string getInfoTip(RegistryKey currentKey)
        {
            IntPtr dataFilePointer;
            string[] infoTipRaw;
            uint stringTableIndex;
            StringBuilder resource;
            string infoTip = "";

            if (currentKey.GetValue("InfoTip") != null)
            {
                infoTipRaw = currentKey.GetValue("InfoTip").ToString().Split(new string[] { ",-" }, StringSplitOptions.None);

                if (infoTipRaw.Length == 2)
                {
                    if (infoTipRaw[0][0] == '@')
                    {
                        infoTipRaw[0] = infoTipRaw[0].Substring(1);
                    }
                    infoTipRaw[0] = Environment.ExpandEnvironmentVariables(infoTipRaw[0]);

                    dataFilePointer = LoadLibraryEx(infoTipRaw[0], IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE); //Load file with strings

                    stringTableIndex = sanitizeUint(infoTipRaw[1]);

                    resource = new StringBuilder(255);
                    LoadString(dataFilePointer, stringTableIndex, resource, resource.Capacity + 1); //Extract needed string
                    FreeLibrary(dataFilePointer);

                    infoTip = resource.ToString();
                }
                else
                {
                    infoTip = currentKey.GetValue("InfoTip").ToString();
                }
            }
            else
            {
                infoTip = "";
            }

            return infoTip;
        }

        public static Icon getIcon(string key, int iconSize)
        {
            var currentKey = clsid.OpenSubKey(key);

            IntPtr iconPtr = IntPtr.Zero;
            List<string> iconString;
            IntPtr dataFilePointer;
            IntPtr iconIndex;
            Icon myIcon = null;

            if (currentKey.OpenSubKey("DefaultIcon") != null)
            {
                if (currentKey.OpenSubKey("DefaultIcon").GetValue(null) != null)
                {
                    iconString = new List<string>(currentKey.OpenSubKey("DefaultIcon").GetValue(null).ToString().Split(new char[] { ',' }, 2));
                    if (iconString[0][0] == '@')
                    {
                        iconString[0] = iconString[0].Substring(1);
                    }

                    dataFilePointer = LoadLibraryEx(iconString[0], IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);

                    if (iconString.Count == 2)
                    {
                        iconIndex = (IntPtr)sanitizeUint(iconString[1]);

                        iconPtr = LoadImage(dataFilePointer, iconIndex, 1, iconSize, iconSize, 0);
                    }

                    if (iconPtr == IntPtr.Zero)
                    {
                        defaultIconPtr = IntPtr.Zero;
                        EnumResourceNamesWithID(dataFilePointer, GROUP_ICON, new EnumResNameDelegate(EnumRes), IntPtr.Zero); //Iterate through resources.

                        iconPtr = LoadImage(dataFilePointer, defaultIconPtr, 1, iconSize, iconSize, 0);
                    }

                    FreeLibrary(dataFilePointer);

                    if (iconPtr != IntPtr.Zero)
                    {
                        try
                        {
                            myIcon = Icon.FromHandle(iconPtr);
                            myIcon = (Icon)myIcon.Clone(); //Remove pointer dependancy.
                        }
                        catch
                        {
                            //Silently fail for now.
                        }
                    }
                }
            }

            if (iconPtr != IntPtr.Zero)
            {
                DestroyIcon(iconPtr);
            }
            return myIcon;
        }

        private static uint sanitizeUint(string args) //Remove all chars before and after first set of digits.
        {
            int x = 0;

            while (x < args.Length && !Char.IsDigit(args[x]))
            {
                args = args.Substring(1);
            }

            x = 0;

            while (x < args.Length && Char.IsDigit(args[x]))
            {
                x++;
            }

            if (x < args.Length)
            {
                args = args.Remove(x);
            }

            return Convert.ToUInt32(args);
        }

        private static bool IS_INTRESOURCE(IntPtr value)
        {
            if (((uint)value) > ushort.MaxValue)
                return false;
            return true;
        }

        private static uint GET_RESOURCE_ID(IntPtr value)
        {
            if (IS_INTRESOURCE(value) == true)
                return (uint)value;
            throw new System.NotSupportedException("value is not an ID!");
        }

        private static string GET_RESOURCE_NAME(IntPtr value)
        {
            if (IS_INTRESOURCE(value) == true)
                return value.ToString();
            return Marshal.PtrToStringUni((IntPtr)value);
        }

        private static bool EnumRes(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam)
        {
            Debug.WriteLine("Type: " + GET_RESOURCE_NAME(lpszType));
            Debug.WriteLine("Name: " + GET_RESOURCE_NAME(lpszName));
            defaultIconPtr = lpszName;
            return false;
        }
    }
}