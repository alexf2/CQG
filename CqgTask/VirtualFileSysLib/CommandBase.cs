using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;

namespace CQG.VirtualFileSys
{
    public sealed partial class FileSysEmulator
    {
        /// <summary>
        /// Ѕазовый класс дл€ команды файловой системы.
        /// </summary>
        /// <remarks>
        /// —татический коструктор ищет всех наследников CommandBase помеченных FsCommandAttribute.
        ///  аждый наследник прив€зан к команде через "public const String NAME".
        /// —татический метод CommandBase.Create должен использоватьс€ дл€ создани€ конкретной команды
        /// по текстовой строке команды с аргументами. 
        ///  онкретные классы команд имеют protected-конструкторы, чтобы их не создавали другими пут€ми.
        ///  аждый конкретный класс команды перегружает метод Execute, чтобы выполнить операцию на файловом менеджере.
        /// </remarks>
        public abstract partial class CommandBase
        {
            #region Static
            private static readonly Regex _exCmdLine = new Regex( @"^\s*(?'cmd'[A-Za-z]+)(?'arg'\s+[^\s]+)*\s*$", 
                RegexOptions.ExplicitCapture|RegexOptions.IgnoreCase|RegexOptions.Singleline|RegexOptions.CultureInvariant );

            private static readonly Regex _exPath = new Regex( @"^[A-Za-z\d\.\\:_+-]+$", 
                RegexOptions.ExplicitCapture|RegexOptions.IgnoreCase|RegexOptions.Singleline|RegexOptions.CultureInvariant );

            private static readonly Dictionary< String, Type > _commandsDescr = new Dictionary< String, Type >();

            static CommandBase()
            {
                Assembly ass = Assembly.GetExecutingAssembly();
                foreach( Type t in ass.GetTypes() )
                    if( t.IsClass && t.GetCustomAttributes(typeof(FsCommandAttribute), true).Length > 0 && t.IsSubclassOf(typeof(CommandBase))  )
                    {
                        FieldInfo fi = t.GetField( "NAME" );
                        if( fi != null && fi.IsLiteral )
                            _commandsDescr.Add( fi.GetRawConstantValue().ToString(), t );
                    }
            }

            public static CommandBase Create( String commandLine )
            {
                CommandBase res = null;

                Match m = _exCmdLine.Match( commandLine );
                if( !m.Success )
                    throw new ApplicationException( String.Format("Unrecognized command format: '{0}'.", commandLine) );

                String commandName = m.Groups[ "cmd" ].Value.ToLower();
                Type commandT;
                if( !_commandsDescr.TryGetValue(commandName, out commandT) )
                    throw new ApplicationException( String.Format("The command '{0}' is unknown.", commandName) );
                
                res = (CommandBase)Activator.CreateInstance( commandT, true );
                

                Int32 numArgs = m.Groups[ "arg" ].Captures.Count;
                if( numArgs < res.NumberArgsMin || numArgs > res.NumberArgsMax )
                    throw new ApplicationException( String.Format("The command '{0}' has improper number of arguments. Must be {1:d} <= N <= {2:d}.", commandLine, res.NumberArgsMin, res.NumberArgsMax) );

                res._arguments = new ArgInfo[ numArgs ];
                Int32 i = 0;
                foreach( Capture arg in m.Groups["arg"].Captures )
                {
                    String pathArg = arg.Value.Trim();
                    if( !_exPath.IsMatch(pathArg) )
                        throw new ApplicationException( String.Format("Argument has unrecognized characters: '{0}'.", arg.Value) );
                    res._arguments[ i++ ] = new ArgInfo( pathArg );
                }

                res.Construct();

                return res;
            }
            #endregion

            #region Protected fields
            protected ArgInfo[] _arguments;
            #endregion

                    
            
            #region Abstracts
            protected abstract void Construct();

            public abstract String Name
            {
                get;
            }
            public abstract Int32 NumberArgsMin
            {
                get;
            }
            public abstract Int32 NumberArgsMax
            {
                get;
            }
            
            public abstract ArgInfo[] PathsMustExist
            {
                get;
            }
            
            public abstract void Execute( FileSysEmulator fse );
            #endregion

            #region Properties
            public ArgInfo[] Arguments
            {
                get { return _arguments; }
            }
            #endregion
        }
    }
}
