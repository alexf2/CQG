using System;
using System.Collections.Generic;
using System.Text;


namespace CQG.VirtualFileSys
{
    /// <summary>
    /// Используется для упрощения поиска классов - конкретных команд файловой системы.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class FsCommandAttribute: Attribute
    {
        public FsCommandAttribute()
        {
        }
    }
}
