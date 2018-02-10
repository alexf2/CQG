using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Configuration;

using CQG.ConsoleBase;
using CQG.VirtualFileSys;


namespace CQG.FileSysManager
{
    /// <summary>
    /// Реализует консольное приложение, получающее на вход текстовый файл с командами виртуальной файловой 
    /// системы и эмулирующее их выполнение при помощи класса FileSysEmulator.
    /// </summary>
    /// <remarks>
    /// Отслеживает ошибки, вызванные конкретной строкой входного файла, и, оборачивая исключение, выброшенное
    /// из FileSysEmulator, добавляет информацию о строке и команде исходного файла, где возникла ошибка.
    /// 
    /// Начальные установки файловой системы определяются в App.config.
    /// </remarks>
    public sealed class FileManagerApplication: ConsoleAppBase
    {
        private String _inputBatchFileName;

        public FileManagerApplication()
        {
        }

        public static Int32 Main( String[] args )
        {
            Int32 res;
            FileManagerApplication app = new FileManagerApplication();
            res = app.ConsoleMain( args );
            Console.Write( "Press a key..." );
            Console.ReadLine();
            return res;
        }

        protected override void ValidateArgs( String[] args )
        {
            if( args.Length != 1 )
                throw new CommandLineArgumentException( args );
        }

        protected override void Construct( String[] args )
        {
            _inputBatchFileName = Path.GetFullPath( args[0] );
            if( !File.Exists(_inputBatchFileName) )
                throw new ApplicationException( String.Format("Input batch file '{0}' isn't found.", _inputBatchFileName) );
        }

        protected override void PrintUsage( TextWriter wr )
        {
            wr.WriteLine( "Uasge:\r\n\tCQG.FileSysManager.exe batch_file_name.txt" );
        }

        protected override void Run()
        {
            FileSysEmulator fs = new FileSysEmulator();
            foreach( String drv in ConfigurationManager.AppSettings["drives"].Split(',') )
                fs.AddDrive( drv.Trim() );

            ArgInfo ai = new ArgInfo( ConfigurationManager.AppSettings["current-drive"] + ":\\" );
            ai.Resolve( fs );
            fs.CurrentDrive = (FileSysEmulator.FsDrive)fs.SearchResolvedPath( ai );


            Int32 lineNumber = 1;
            using( FileStream stm = new FileStream(_inputBatchFileName, FileMode.Open,  FileAccess.Read) )
                using( StreamReader rd = new StreamReader(stm) )
                    while( !rd.EndOfStream )
                    {
                        String cmdText = rd.ReadLine();
                        if( cmdText.Trim().Length > 0 )
                        {                            
                            try
                            {
                                FileSysEmulator.CommandBase cmd = FileSysEmulator.CommandBase.Create( cmdText );
                                fs.ExecuteCommandAgainstFileSystem( cmd );
                            }
                            catch( Exception e )
                            {
                                throw new ApplicationException( String.Format("Command '{0}' at line '{1:d}' is failed.", cmdText, lineNumber), e );
                            }
                        }
                        ++lineNumber;
                    }

            XmlDocument doc = fs.GetFileSystemContent();
            fs.PrintTo( Console.Out );
        }
    }
}
