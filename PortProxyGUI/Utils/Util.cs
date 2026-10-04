using Microsoft.Win32;
using PortProxyGUI.Data;
using PortProxyGUI.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PortProxyGUI.Utils;

public static class Util
{
    internal static readonly string ServiceName = "iphlpsvc";
    internal static readonly string ServiceFriendlyName = "IP Helper";

    private static InvalidOperationException InvalidPortProxyType(string type) => new($"Invalid port proxy type ({type}).");
    private static readonly string[] ProxyTypes = ["v4tov4", "v4tov6", "v6tov4", "v6tov6"];

    private static string GetKeyName(string type)
    {
        return $@"SYSTEM\CurrentControlSet\Services\PortProxy\{type}\tcp";
    }

    public static Rule[] GetProxies()
    {
        var ruleList = new List<Rule>();
        foreach (var type in ProxyTypes)
        {
            var keyName = GetKeyName(type);
            using var key = Registry.LocalMachine.OpenSubKey(keyName);

            if (key is not null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var listenParts = name.Split('/');
                    if (listenParts.Length != 2) continue;
                    var listenOn = listenParts[0];
                    if (!int.TryParse(listenParts[1], out var listenPort)) continue;

                    var rawValue = key.GetValue(name)?.ToString();
                    if (string.IsNullOrWhiteSpace(rawValue)) continue;
                    var connectParts = rawValue.Split('/');
                    if (connectParts.Length != 2) continue;
                    var connectTo = connectParts[0];
                    if (!int.TryParse(connectParts[1], out var connectPort)) continue;

                    ruleList.Add(new Rule
                    {
                        Type = type,
                        ListenOn = listenOn,
                        ListenPort = listenPort,
                        ConnectTo = connectTo,
                        ConnectPort = connectPort,
                    });
                }
            }
        }
        return [.. ruleList];
    }

    public static void AddOrUpdateProxy(Rule rule)
    {
        // $"netsh interface portproxy add {rule.Type} listenaddress={rule.ListenOn} listenport={rule.ListenPort} connectaddress={rule.ConnectTo} connectport={rule.ConnectPort}"

        if (!ProxyTypes.Contains(rule.Type)) throw InvalidPortProxyType(rule.Type);

        var keyName = GetKeyName(rule.Type);
        var name = $"{rule.ListenOn}/{rule.ListenPort}";
        var value = $"{rule.ConnectTo}/{rule.ConnectPort}";

        using var key = Registry.LocalMachine.CreateSubKey(keyName, true);
        if (key is null) throw new InvalidOperationException($"Cannot open portproxy registry key: {keyName}");
        key.SetValue(name, value);
    }

    public static void DeleteProxy(Rule rule)
    {
        // $"netsh interface portproxy delete {rule.Type} listenaddress={rule.ListenOn} listenport={rule.ListenPort}"

        if (!ProxyTypes.Contains(rule.Type)) throw InvalidPortProxyType(rule.Type);

        var keyName = GetKeyName(rule.Type);
        using var key = Registry.LocalMachine.OpenSubKey(keyName, true);
        var name = $"{rule.ListenOn}/{rule.ListenPort}";

        // Missing rules are harmless, but access/write errors must reach the caller.
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public static bool IsServiceRunning()
    {
        var hManager = NativeMethods.OpenSCManager(null, null, (uint)GenericRights.GENERIC_READ);
        if (hManager == IntPtr.Zero) throw new InvalidOperationException("Open SC Manager failed.");

        var hService = NativeMethods.OpenService(hManager, ServiceName, ServiceRights.SERVICE_QUERY_STATUS);
        if (hService == IntPtr.Zero)
        {
            NativeMethods.CloseServiceHandle(hManager);
            throw new InvalidOperationException($"Open Service ({ServiceName}) failed.");
        }

        var status = new ServiceStatus();
        NativeMethods.QueryServiceStatus(hService, ref status);

        NativeMethods.CloseServiceHandle(hService);
        NativeMethods.CloseServiceHandle(hManager);

        return status.dwCurrentState == ServiceState.SERVICE_RUNNING;
    }

    public static void StartService()
    {
        var hManager = NativeMethods.OpenSCManager(null, null, (uint)GenericRights.GENERIC_READ | (uint)ScmRights.SC_MANAGER_CONNECT);
        if (hManager == IntPtr.Zero) throw new InvalidOperationException("Open SC Manager failed.");

        var hService = NativeMethods.OpenService(hManager, ServiceName, ServiceRights.SERVICE_START);
        if (hService == IntPtr.Zero)
        {
            NativeMethods.CloseServiceHandle(hManager);
            throw new InvalidOperationException($"Open Service ({ServiceName}) failed.");
        }

        NativeMethods.StartService(hService, 0, null);

        NativeMethods.CloseServiceHandle(hService);
        NativeMethods.CloseServiceHandle(hManager);
    }

    public static void ParamChange()
    {
        var hManager = NativeMethods.OpenSCManager(null, null, (uint)GenericRights.GENERIC_READ);
        if (hManager == IntPtr.Zero) throw new InvalidOperationException("Open SC Manager failed.");

        var hService = NativeMethods.OpenService(hManager, ServiceName, ServiceRights.SERVICE_PAUSE_CONTINUE);
        if (hService == IntPtr.Zero)
        {
            NativeMethods.CloseServiceHandle(hManager);
            throw new InvalidOperationException($"Open Service ({ServiceName}) failed.");
        }

        var status = new ServiceStatus();
        NativeMethods.ControlService(hService, ServiceControls.SERVICE_CONTROL_PARAMCHANGE, ref status);

        NativeMethods.CloseServiceHandle(hService);
        NativeMethods.CloseServiceHandle(hManager);
    }

}
