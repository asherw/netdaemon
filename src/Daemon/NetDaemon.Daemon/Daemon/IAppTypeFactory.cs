using NetDaemon.Common;

namespace NetDaemon.Daemon
{
    public interface IAppTypeFactory
    {
        INetDaemonAppBase? ResolveByClassName(string className);
    }
}