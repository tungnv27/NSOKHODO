namespace NSOKHODO.Client
{
    public enum ClientState
    {
        Disconnected,
        Connecting,
        Handshake,
        LoggingIn,
        DataSync,
        SelectingChar,
        InGame,
        Dead,
        Reconnecting,
        Error
    }
}
