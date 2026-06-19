param([string]$ExePath, [string]$IcoPath)

if (-not (Test-Path $ExePath)) { exit 1 }
if (-not (Test-Path $IcoPath)) { exit 1 }

$code = @'
using System;
using System.IO;
using System.Runtime.InteropServices;

public class IconInjector {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExistingResources);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, int cbData);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

    public static bool Inject(string exePath, string icoPath) {
        byte[] icoData = File.ReadAllBytes(icoPath);
        ushort type = BitConverter.ToUInt16(icoData, 2);
        if (type != 1) return false;
        ushort count = BitConverter.ToUInt16(icoData, 4);
        
        IntPtr hUpdate = BeginUpdateResource(exePath, true);
        if (hUpdate == IntPtr.Zero) return false;
        
        IntPtr RT_GROUP_ICON = (IntPtr)14;
        IntPtr RT_ICON = (IntPtr)3;
        
        byte[] groupData = new byte[6 + 14 * count];
        Array.Copy(icoData, 0, groupData, 0, 6);
        for (int i = 0; i < count; i++) {
            int hdrOff = 6 + i * 16;
            groupData[6 + i * 14 + 0] = icoData[hdrOff];
            groupData[6 + i * 14 + 1] = icoData[hdrOff + 1];
            groupData[6 + i * 14 + 2] = icoData[hdrOff + 2];
            groupData[6 + i * 14 + 3] = icoData[hdrOff + 3];
            BitConverter.GetBytes(BitConverter.ToInt16(icoData, hdrOff + 4)).CopyTo(groupData, 6 + i * 14 + 4);
            BitConverter.GetBytes(BitConverter.ToInt16(icoData, hdrOff + 6)).CopyTo(groupData, 6 + i * 14 + 6);
            int size = BitConverter.ToInt32(icoData, hdrOff + 8);
            int dataOff = BitConverter.ToInt32(icoData, hdrOff + 12);
            BitConverter.GetBytes(size).CopyTo(groupData, 6 + i * 14 + 8);
            BitConverter.GetBytes((short)(i + 1)).CopyTo(groupData, 6 + i * 14 + 12);
            
            byte[] iconEntry = new byte[size];
            Array.Copy(icoData, dataOff, iconEntry, 0, size);
            UpdateResource(hUpdate, RT_ICON, (IntPtr)(i + 1), 0x0809, iconEntry, size);
        }
        UpdateResource(hUpdate, RT_GROUP_ICON, (IntPtr)1, 0x0809, groupData, groupData.Length);
        return EndUpdateResource(hUpdate, false);
    }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies 'System.dll' -IgnoreWarnings
[IconInjector]::Inject($ExePath, $IcoPath)
