using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;

namespace CQG.VirtualFileSys
{   
    /// <summary>
    /// Представляет аргумент операции файловой системы: имя файла, каталога, линка, диска, полный путь.
    /// </summary>
    /// <remarks>
    /// Перед использованием в операциях нужно вызвать метод Resolve, передав ему менеджер
    /// виртуальной файловой системы. При этом создаётся копия объекта, которая сохраняется в
    /// свойстве Resolved. Новый объект является полным абсолютным путём относительно корня диска.
    /// Чтобы получить элемент файловой системы, на который указывает этот объект, нужно использовать
    /// метод FileSysEmulator.SearchResolvedPath. При этом производится поиск элемента и уточнение
    /// его типа (каталог/файл).
    /// Хинт для уточнения типа пути указывается через свойство PathTarget.
    /// </remarks>
    public sealed class ArgInfo
    {
        #region Enums and constants
        public enum TargetType
        {
            Empty,
            Dir, //ends with '\' or ':'
            FileOrDir, //should be tested
            File
        }
        public enum PathType
        {
            Undefined,

            RelativeToDriveDir, // z:, z:xxx, z:xxx\
            RelativeToDir, // xxx, xxx\

            RelativeToDriveRoot, // z:\, z:\xxx
            RelativeToRoot // \xxx
        }
        #endregion

        #region Static
        private static readonly Regex _exPath = new Regex( @"^(?'drive'[A-Za-z]:){0,1}(?'path'(\\){0,1}([A-Za-z\d\._+-]+){0,1})*$", 
            RegexOptions.ExplicitCapture|RegexOptions.IgnoreCase|RegexOptions.Singleline|RegexOptions.CultureInvariant );

        private static readonly Regex _exPathStep = new Regex( @"^[A-Za-z\d\\:_+-]{1,8}(\.[A-Za-z\d\\:_+-]{0,3}){0,1}$", 
            RegexOptions.ExplicitCapture|RegexOptions.IgnoreCase|RegexOptions.Singleline|RegexOptions.CultureInvariant );

        private static ArgInfo _currDirOfCurrDrive;

        static ArgInfo()
        {
            ArgInfo res = new ArgInfo();
            res._arg = String.Empty;
            res._relativity = PathType.RelativeToDir;
            res._pathTarget = TargetType.Dir;
            _currDirOfCurrDrive = res;
        }

        public static ArgInfo CurrDirOfCurrDrive
        {
            get { return _currDirOfCurrDrive; }
        }
        #endregion


        #region Private fields
        private String _arg;
        private PathType _relativity = PathType.Undefined;
        private TargetType _pathTarget = TargetType.Empty;

        private String _drive = String.Empty;
        private String[] _locationSteps = new String[ 0 ];

        private ArgInfo _resolved;
        #endregion


        #region Public fields
        public FileSysEmulator.FileSystemItem ResolvedFsi;
        #endregion

        #region Constructors
        private ArgInfo()
        {
        }

        public ArgInfo( String a )
        {
            Match m = _exPath.Match( a );
            if( !m.Success )
                throw new ApplicationException( String.Format("Unrecognized path '{0}'.", a) );

            _arg = a;

            if( a.EndsWith("\\") || a.EndsWith(":") )
                _pathTarget = TargetType.Dir;
            else if( a.Length > 0 )
                _pathTarget = TargetType.FileOrDir;


            if( _pathTarget != TargetType.Empty )
            {
                if( m.Groups["drive"].Success )
                    _drive = m.Groups["drive"].Value.Substring( 0, 1 );

                CaptureCollection capts = m.Groups[ "path" ].Captures;
                if( capts.Count > 0 )
                {
                    Boolean isRooted = false; 
                    List<String> tmpSteps = new List<String>();
                    for( Int32 i = 0; i < capts.Count; ++i )                        
                    {
                        String c = capts[ i ].Value;
                        if( i == 0 && c.StartsWith("\\") )
                            isRooted = true;

                        c = c.Replace( "\\", String.Empty );
                        if( c.Length > 0 )
                        {
                            if( !_exPathStep.IsMatch(c) )
                                throw new ApplicationException( String.Format("File or directory doesn't match to the 8.3 pattern: '{0}'.", c) );
                            tmpSteps.Add( c );
                        }
                    }

                    if( tmpSteps.Count > 0 )                        
                        _locationSteps = tmpSteps.ToArray();

                    if( isRooted )
                        _relativity = _drive.Length > 0 ? PathType.RelativeToDriveRoot:PathType.RelativeToRoot;
                    else
                        _relativity = _drive.Length > 0 ? PathType.RelativeToDriveDir:PathType.RelativeToDir;
                }

                if( _locationSteps.Length == 0 && _drive.Length > 0 )
                    _relativity = PathType.RelativeToDriveRoot; //correct 'C:' to 'C:\'
            }
        }
        #endregion

        #region Properties
        public ArgInfo Resolved
        {
            get { return _resolved; }
        }
        
        public String Arg
        {
            get { return _arg; }
        }
        public String ArgForSearch
        {
            get { return _arg.ToLower(); }
        }

        public PathType Relativity
        {
            get { return _relativity; }
        }
        public TargetType PathTarget
        {
            get { return _pathTarget; }
            set { _pathTarget = value; }
        }

        public String Drive
        {
            get { return _drive; }
        }
        public String DriveForSearch
        {
            get { return _drive.ToLower(); }
        }

        public String[] Steps
        {
            get { return _locationSteps; }
        }
        public String[] StepsForSearch
        {
            get 
            { 
                String[] res = new String[ _locationSteps.Length ];
                for( Int32 i = 0; i < res.Length; ++ i )                    
                    res[i] = _locationSteps[i].ToLower() + (i < res.Length - 1 || _pathTarget == TargetType.Dir ? "_d":String.Empty);
                
                return res;
            }
        }

        public String LastStep
        {
            get { return _locationSteps.Length == 0 ? String.Empty:_locationSteps[ _locationSteps.Length - 1 ]; }
        }
        public String LastStepForSearch
        {
            get { return LastStep.ToLower(); }
        }
        #endregion


        #region Methods
        public override String ToString()
        {
            return _arg;
        }

        public String GetFullPath()
        {
            StringBuilder bld = new StringBuilder();
            if( _drive.Length > 0 )
                bld.AppendFormat( "{0}:", _drive );
            if( _relativity == PathType.RelativeToDriveRoot || _relativity == PathType.RelativeToRoot )
                bld.Append( "\\" );
            for( Int32 i = 0; i < _locationSteps.Length; ++i )
            {
                if( i > 0 ) 
                    bld.Append( "\\" );
                bld.Append( _locationSteps[i] );
            }
            if( _pathTarget == TargetType.Dir && !bld.ToString().EndsWith("\\") )
                bld.Append( "\\" );

            String res = bld.ToString();
            return res == "\\" ? String.Empty:res;
        }

        public void Resolve( FileSysEmulator fse )
        {
            FileSysEmulator.FsDrive currDrv = fse.CurrentDrive;
            if( currDrv == null )
                throw new ApplicationException( String.Format("Can't resolve path '{0}': there isn't current drive.", ToString()) );
            
            _resolved = (ArgInfo)this.MemberwiseClone();
            _resolved._locationSteps = new String[ 0 ];

            if( _drive.Length == 0 )
                _resolved._drive = currDrv.Name;

            if( _relativity == PathType.RelativeToDir || _relativity == PathType.RelativeToDriveDir )
            {
                _resolved._relativity = PathType.RelativeToDriveRoot;
                FileSysEmulator.FsDir currDir = currDrv.CurrentDir;
                if( currDir != null )
                {
                    List<String> lst = currDir.GetLocationSteps( false );
                    lst.AddRange( _locationSteps );
                    _resolved._locationSteps = lst.ToArray();
                }
                else
                    _resolved._locationSteps = _locationSteps;
            }
            else
                _resolved._locationSteps = _locationSteps;

            
            _resolved._arg = _resolved.GetFullPath();
        }

        public void SeparateLastStep( out ArgInfo ai, TargetType pathTargetLeft, TargetType pathTargetRight )
        {
            ai = new ArgInfo();
            ai._relativity = PathType.RelativeToDir;
            ai._pathTarget = pathTargetRight;
            ai._arg = _locationSteps[ _locationSteps.Length - 1 ];
            ai._locationSteps = new String[ 1 ]{ _locationSteps[_locationSteps.Length - 1] };
            ai._arg = ai.GetFullPath();

            _pathTarget = pathTargetLeft;
            Array.Resize<String>( ref _locationSteps, _locationSteps.Length - 1 );
            _arg = GetFullPath();
        }
        #endregion

    }
}
 