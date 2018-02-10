using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.Configuration;

namespace CQG.ConsoleBase
{
    /// <summary>
    /// Представляет базовый класс консольного приложения.
    /// </summary>    
    public abstract class ConsoleAppBase
    {
        protected const Int32 RETCODE_BAD_USAGE = -1;
        protected const Int32 RETCODE_GENERIC_EXCEPTION = -2;
        protected const Int32 RETCODE_OK = 0;

        protected abstract void ValidateArgs( String[] args );
        protected abstract void Construct( String[] args );
        protected abstract void Run();
        protected abstract void PrintUsage( TextWriter wr );

        public Int32 ConsoleMain( String[] args )
        {            
            try
            {                
                ValidateArgs( args );
                Construct( args );
                Run();
            }
            catch( CommandLineArgumentException  )
            {
                Console.WriteLine( "Invalid arguments." );
                PrintUsage( Console.Out );
                return RETCODE_BAD_USAGE;
            }
            catch( Exception e )
            {
                DumpException( e );
                return RETCODE_GENERIC_EXCEPTION;
            }
            return RETCODE_OK;
        }

        protected static void DumpException( Exception ex )
        {
            if( ConfigurationManager.AppSettings["exceptions-full-info"] == "1" )
                Console.WriteLine( "Exception of type '{0}' is thrown:", ex.GetType().Name );

            for( Int32 level = 0; ex!= null; ex = ex.InnerException )            
                DumpExInternal( ex, level++ );
        }
        private static void DumpExInternal( Exception ex, Int32 lvl )
        {
            String padding = new String( ' ', lvl * 2 );
            if( ConfigurationManager.AppSettings["exceptions-full-info"] == "1" )
                Console.WriteLine( "{2}***Error: [{0}]; from [{1}]", ex.Message, ex.Source, padding );
            else
                Console.WriteLine( "{1}***Error: [{0}]", ex.Message, padding );

            if( ConfigurationManager.AppSettings["exceptions-PrintStack"] == "1" )
                Console.WriteLine( "{1}Stack: {0}", ex.StackTrace, padding );
            Console.WriteLine();
        }
    }
}
