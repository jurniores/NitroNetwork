using NitroNetwork.Core;
namespace NitroNetwork.Core
{
    public interface INitroVar
    {
        internal void SetConfig(byte Id, NitroIdentity identity);
        internal void ReadVar(NitroBuffer _buffer);
        internal void Send(Target target = Target.Self, NitroConn conn = null);
    }

}