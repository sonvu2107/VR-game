using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

public static class LanNetworkAddresses
{
    // Refresh the actual adapters, not a cached hostname/DNS address list.
    public static string Describe()
    {
        var primary = new List<string>();
        var secondary = new List<string>();
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            IPInterfaceProperties properties = adapter.GetIPProperties();
            bool hasGateway = false;
            foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
                if (gateway.Address.AddressFamily == AddressFamily.InterNetwork && gateway.Address.ToString() != "0.0.0.0") hasGateway = true;
            foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address.Address) ||
                    address.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal)) continue;
                string entry = adapter.Name + ": " + address.Address;
                if (hasGateway) primary.Add(entry);
                else secondary.Add(entry);
            }
        }
        // A secondary address remains useful when the Client joined the Host's hotspot.
        if (primary.Count == 0) primary.AddRange(secondary);
        return primary.Count > 0 ? string.Join(" / ", primary) : "Xem IPv4 bằng ipconfig";
    }
}
