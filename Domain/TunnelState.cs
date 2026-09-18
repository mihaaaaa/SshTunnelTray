namespace SshTunnelTray.Domain;

public enum TunnelState
{
    Stopped,
    Connecting,
    Connected,
    Reconnecting,
    Error
}
