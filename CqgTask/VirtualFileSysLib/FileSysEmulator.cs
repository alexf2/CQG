using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using System.Xml;
using System.Configuration;
using System.Threading;

namespace CQG.VirtualFileSys
{
    /// <summary>
    /// Представляет эмелятор файловой системы.
    /// </summary>
    /// <remarks>
    /// Хранит дерево файловой системы в поле _root.
    /// Плоское представление хранится в _allNodes: все элементы файловой системы по уникальному ID.
    /// 
    /// Отвечает за выдачу уникальных ID элементам файловой системы (GetNextId).
    /// Координирует иподдерживает выполнение команд файловой системы (ExecuteCommandAgainstFileSystem).
    /// Отвечает за поиск элемента файловой системы по его полному пути (SearchResolvedPath).
    /// Реализует ремэппинг линков при копировании поддеревьев файловой системы (StartCopying, EndCopying).
    /// 
    /// Доступные диски системы и текущий диск инициализируются через App.config drives и current-drive.
    /// </remarks>
    public sealed partial class FileSysEmulator
    {
        private static Int64 _idCount = 0;
        private Dictionary< Int64, FileSystemItem > _allNodes = new Dictionary< Int64, FileSystemItem >();

        private FsRoot _root;

        private FileSystemItem _currentMovingRoot;
        private List<FsLink> _linksToFlip = new List<FsLink>();
        private Dictionary<Int64, Int64> _copyingMapOfIDs = new Dictionary<Int64, Int64>();
        
        private void StartCopying( FileSystemItem rootToCopy )
        {
            _currentMovingRoot = rootToCopy;
            _linksToFlip.Clear();
            _copyingMapOfIDs.Clear();
        }
        private void EndCopying()
        {
            foreach( FsLink lnk in _linksToFlip )
                lnk.FlipLinkTo( _allNodes[ _copyingMapOfIDs[lnk.ItemTo.ID] ] );

            _linksToFlip.Clear();
            _copyingMapOfIDs.Clear();
        }

        private FileSystemItem RegisterItem( FileSystemItem item )
        {
            if( _allNodes.ContainsKey(item.ID) )
                throw new ApplicationException( String.Format("The {0} with id = '{1}' already exists.", item.ToString(), item.ID) );
            _allNodes.Add( item.ID, item );

            return item;
        }
        private FileSystemItem UnregisterItem( FileSystemItem item )
        {
            if( !_allNodes.ContainsKey(item.ID) )
                throw new ApplicationException( String.Format("The {0} with id = '{1}' doesn't exist.", item.ToString(), item.ID) );
            _allNodes.Remove( item.ID );

            return item;
        }
        

        private static Int64 GetNextId()
        {
            return Interlocked.Increment( ref _idCount );
        }

        public FileSysEmulator()
        {
            _root = (FsRoot)RegisterItem( new FsRoot(this) );
        }

        public FsDrive AddDrive( String letter )
        {
            FsDrive res = _root.AddDrive( letter );
            return res;
        }

        public XmlDocument GetFileSystemContent()
        {
            return _root.GetAsXml();
        }

        public FsDrive CurrentDrive
        {
            get { return _root.CurrentDrive; }
            set
            {
                if( value.Owner != this || value.Parent != _root )
                    throw new ApplicationException( "Can't set current drive. The provided drive either belongs to another FS or isn't added." );
                _root.CurrentDrive = value;
            }
        }

        public FileSystemItem SearchResolvedPath( ArgInfo ai )
        {
            ArgInfo aiRes = ai.Resolved;
            FileSystemItem item = null;

            List<String> locationSteps = new List<String>();
            locationSteps.Add( aiRes.DriveForSearch );
            locationSteps.AddRange( aiRes.StepsForSearch );

            FsContainer cont = _root;
            for( Int32 i = 0; i < locationSteps.Count; ++i )
            {
                String step = locationSteps[ i ];
                item = cont[ step ];
                if( item == null && i == locationSteps.Count - 1 && aiRes.PathTarget == ArgInfo.TargetType.FileOrDir )
                {
                    step += "_d";
                    item = cont[ step ];
                    if( item != null && !(item is FsDir) )
                        item = null;
                }
                if( item == null )
                    //throw new ApplicationException( String.Format("Can't locate path '{0}'.", aiRes.Arg) );
                    break;

                if( item is FsContainer )
                    cont = (FsContainer)item;
                else if( item is FsLink )
                {
                    FileSystemItem itTo = GetTargetItemOfLink( (FsLink)item );
                    if( itTo is FsContainer )
                    {
                        if( i != locationSteps.Count - 1 )
                            item = cont = (FsContainer)itTo;
                    }
                    else if( i != locationSteps.Count - 1 )
                    {
                        //throw new ApplicationException( String.Format("Can't locate path '{0}'. There isn't directory under the link '{1}'.", aiRes.Arg, itTo.ToString()) );
                        item = null;
                        break;
                    }
                }
                else if( i != locationSteps.Count - 1 )
                {
                    //throw new ApplicationException( String.Format("Can't locate path '{0}'. There isn't directory.", aiRes.Arg) );
                    item = null;
                    break;
                }
            }
            if( item != null && ai.PathTarget == ArgInfo.TargetType.FileOrDir )
            {
                FileSystemItem itTest;
                if( item is FsLink )
                    itTest = GetTargetItemOfLink( (FsLink)item );
                else
                    itTest = item;

                ai.PathTarget = (item is FsDir ? ArgInfo.TargetType.Dir:ArgInfo.TargetType.File);
                aiRes.PathTarget = ai.PathTarget;
            }
            return item;
        }

        //((FileSysEmulator.FsContainer)item.Parent).RemoveItem( item );
        private void RemoveItem( FileSystemItem item )
        {
            if( item is FsRoot )
                throw new ApplicationException( "Cant' remove file system root." );
            if( item is FsDrive )
                throw new ApplicationException( "Cant' remove file system drive." );

            FsContainer cont = (FsContainer)item.Parent;
            cont.RemoveItem( item );
        }

        public FileSystemItem GetTargetItemOfLink( FsLink lnk )
        {
            return _allNodes[ lnk.ItemTo.ID ];
        }

        private void ValidateAndCorrectArguments( CommandBase cmd )
        {
            ArgInfo[] ais = cmd.PathsMustExist;
            foreach( ArgInfo ai in ais )
            {
                ai.Resolve( this );
                FileSystemItem item = SearchResolvedPath( ai );
                if( item == null )
                    throw new ApplicationException( String.Format("The {1} '{0}' isn't found.", ai.Arg, ai.PathTarget == ArgInfo.TargetType.Dir ? "directory":"file" ) );
                ai.Resolved.ResolvedFsi = item;
            }
        }

        public void ExecuteCommandAgainstFileSystem( CommandBase cmd )
        {
            ValidateAndCorrectArguments( cmd );

            cmd.Execute( this );            
        }

        public void PrintTo( TextWriter wr )
        {
            PrintCtx ctx = new PrintCtx();
            _root.PrintTo( wr, ctx, false );
        }
    }

}
