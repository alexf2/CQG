using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace CQG.ConsoleBase
{
    [Serializable]
    public class CommandLineArgumentException: ArgumentException
    {
        private String[] _args;

        public CommandLineArgumentException()
        {
        }
        public CommandLineArgumentException( String[] args ): base()
        {            
            _args = args;
        }

        public String[] Args
        {
            get { return _args; }
        }

    }
}
