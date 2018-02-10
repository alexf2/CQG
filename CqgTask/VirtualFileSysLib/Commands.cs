using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;

namespace CQG.VirtualFileSys
{
    public sealed partial class FileSysEmulator
    {
        [FsCommand]
        public class CommandMD: CommandBase
        {
            public const String NAME = "md";

            private ArgInfo _targetDir, _newSubDir;

            public override String Name
            {
                get { return NAME; }
            }
            public override Int32 NumberArgsMin
            {
                get { return 1; }
            }
            public override Int32 NumberArgsMax
            {
                get { return 1; }
            }

            protected CommandMD() {}

            protected override void Construct()
            {
                _targetDir = _arguments[ 0 ];
                if( _targetDir.Steps.Length == 0 )
                    throw new ApplicationException( String.Format("Can't create directory '{0}'. Name is missing.", _targetDir.Arg) );

                _targetDir.SeparateLastStep( out _newSubDir, ArgInfo.TargetType.Dir, ArgInfo.TargetType.Dir );
            }

            public override ArgInfo[] PathsMustExist
            {
                get { return new ArgInfo[]{ _targetDir }; }
            }        

            public override void Execute( FileSysEmulator fse )
            {
                FileSysEmulator.FileSystemItem item = _targetDir.Resolved.ResolvedFsi;

                if( item is FileSysEmulator.FsLink )
                    item = fse.GetTargetItemOfLink( (FileSysEmulator.FsLink)item );

                if( item is FileSysEmulator.FsDrive )
                {
                    FileSysEmulator.FsDrive drv = (FileSysEmulator.FsDrive)item;
                    drv.AddDir( _newSubDir.Arg.Replace("\\", "") );
                }
                else
                {
                    FileSysEmulator.FsDir dir = (FileSysEmulator.FsDir)item;
                    dir.AddDir( _newSubDir.Arg.Replace("\\", "") );
                }
            }
        }

        [FsCommand]
        public class CommandCD: CommandBase
        {
            public const String NAME = "cd";

            private ArgInfo _newCurrDir;

            public override String Name
            {
                get { return NAME; }
            }
            public override Int32 NumberArgsMin
            {
                get { return 1; }
            }
            public override Int32 NumberArgsMax
            {
                get { return 1; }
            }

            protected CommandCD() {}

            protected override void Construct()
            {
                _newCurrDir = _arguments[ 0 ];
                _newCurrDir.PathTarget = ArgInfo.TargetType.Dir;
            }

            public override ArgInfo[] PathsMustExist
            {
                get { return new ArgInfo[]{ _newCurrDir }; }
            }

            public override void Execute( FileSysEmulator fse )
            {
                FileSysEmulator.FileSystemItem item = _newCurrDir.Resolved.ResolvedFsi;
                if( item is FileSysEmulator.FsLink )
                    item = fse.GetTargetItemOfLink( (FileSysEmulator.FsLink)item );

                fse.CurrentDrive = item.GetParentDrive();
                if( item is FileSysEmulator.FsDir )
                    fse.CurrentDrive.CurrentDir = (FileSysEmulator.FsDir)item;
                else if( item is FileSysEmulator.FsDrive )
                    fse.CurrentDrive.CurrentDir = null;
            }
        }

        [FsCommand]
        public class CommandRD: CommandBase
        {
            public const String NAME = "rd";

            private ArgInfo _dirPath;

            public override String Name
            {
                get { return NAME; }
            }
            public override Int32 NumberArgsMin
            {
                get { return 1; }
            }
            public override Int32 NumberArgsMax
            {
                get { return 1; }
            }

            protected CommandRD() {}

            protected override void Construct()
            {
                _dirPath = _arguments[ 0 ];
                _dirPath.PathTarget = ArgInfo.TargetType.Dir;
            }

            public override ArgInfo[] PathsMustExist
            {
                get { return new ArgInfo[]{ _dirPath }; }
            }
            
            public override void Execute( FileSysEmulator fse )
            {
                FileSysEmulator.FileSystemItem item = _dirPath.Resolved.ResolvedFsi;
                if( item is FileSysEmulator.FsLink )
                    item = fse.GetTargetItemOfLink( (FileSysEmulator.FsLink)item );

                FileSysEmulator.FsDir dir = (FileSysEmulator.FsDir)item;
                if( !dir.IsEmpty )
                    throw new ApplicationException( String.Format("Can't remove '{0}' because it isn't empty.", item.GetFullPath()) );
                if( dir == fse.CurrentDrive.CurrentDir )
                    throw new ApplicationException( String.Format("Can't remove '{0}' because it is current.", item.GetFullPath()) );

                fse.RemoveItem( item );
            }
        }

        [FsCommand]
        public class CommandDelTree: CommandBase
        {
            public const String NAME = "deltree";

            private ArgInfo _treeToRemove;

            public override String Name
            {
                get { return NAME; }
            }
            public override Int32 NumberArgsMin
            {
                get { return 1; }
            }
            public override Int32 NumberArgsMax
            {
                get { return 1; }
            }

            protected CommandDelTree() {}

            protected override void Construct()
            {
                _treeToRemove = _arguments[ 0 ];
                _treeToRemove.PathTarget = ArgInfo.TargetType.Dir;
            }

            public override ArgInfo[] PathsMustExist
            {
                get { return new ArgInfo[]{ _treeToRemove }; }
            }

            public override void Execute( FileSysEmulator fse )
            {
                FileSysEmulator.FileSystemItem item = _treeToRemove.Resolved.ResolvedFsi;
                if( item is FileSysEmulator.FsLink )
                    item = fse.GetTargetItemOfLink( (FileSysEmulator.FsLink)item );

                if( item is FileSysEmulator.FsDrive )
                    throw new ApplicationException( String.Format("Can't remove '{0}'.", item.GetFullPath()) );

                FileSysEmulator.FsDir dir = (FileSysEmulator.FsDir)item;

                if( fse.CurrentDrive.CurrentDir != null )
                {
                    FileSysEmulator.FileSystemItem drT = fse.CurrentDrive.CurrentDir;
                    Boolean found = false;
                    for( ; drT != null && !(drT is FileSysEmulator.FsRoot); drT = drT.Parent )
                        if( drT == dir )
                        {
                            found = true; break;
                        }
                    if( found )
                        throw new ApplicationException( String.Format("Can't remove '{0}' with subtree because it contains the current directory.", item.GetFullPath()) );
                }

                fse.RemoveItem( item );
            }
        }

        [FsCommand]
        public class CommandMF: CommandBase
        {
            public const String NAME = "mf";

            private ArgInfo _pathWhereToMake, _fileName;

            public override String Name
            {
                get { return NAME; }
            }
            public override Int32 NumberArgsMin
            {
                get { return 1; }
            }
            public override Int32 NumberArgsMax
            {
                get { return 1; }
            }

            protected CommandMF() {}

            protected override void Construct()
            {
                _pathWhereToMake = _arguments[ 0 ];
                if( _pathWhereToMake.Steps.Length == 0 )
                    throw new ApplicationException( String.Format("Can't create a file '{0}'. Name is missing.", _pathWhereToMake.Arg) );

                _pathWhereToMake.SeparateLastStep( out _fileName, ArgInfo.TargetType.Dir, ArgInfo.TargetType.File );
            }

            public override ArgInfo[] PathsMustExist
            {
                get { return new ArgInfo[]{ _pathWhereToMake }; }
            }

            public override void Execute( FileSysEmulator fse )
            {
                FileSysEmulator.FileSystemItem dirWhere = _pathWhereToMake.Resolved.ResolvedFsi;

                if( dirWhere is FileSysEmulator.FsLink )
                    dirWhere = fse.GetTargetItemOfLink( (FileSysEmulator.FsLink)dirWhere );

                if( dirWhere is FileSysEmulator.FsDir )
                    ((FileSysEmulator.FsDir)dirWhere).AddFile( _fileName.Arg );
                else if( dirWhere is FileSysEmulator.FsDrive )
                    ((FileSysEmulator.FsDrive)dirWhere).AddFile( _fileName.Arg );
                else
                    throw new ApplicationException( String.Format("Can't create file '{0}' in '{1}'.", _fileName.Arg, dirWhere.ToString()) );
            }
        }

        [FsCommand]
        public class CommandMhl: CommandBase
        {
            public const String NAME = "mhl";

            private ArgInfo _sourcePath, _destPath;

            public override String Name
            {
                get { return NAME; }
            }
            public override Int32 NumberArgsMin
            {
                get { return 1; }
            }
            public override Int32 NumberArgsMax
            {
                get { return 2; }
            }

            protected CommandMhl() {}

            protected override void Construct()
            {
                _sourcePath = _arguments[ 0 ];
                if( _arguments.Length == 2 )
                    _destPath = _arguments[ 1 ];
                else            
                    _destPath = ArgInfo.CurrDirOfCurrDrive;

                _destPath.PathTarget = ArgInfo.TargetType.Dir;
            }

            public override ArgInfo[] PathsMustExist
            {
                get { return new ArgInfo[]{ _sourcePath, _destPath }; }
            }

            public override void Execute( FileSysEmulator fse )
            {
                FileSysEmulator.FileSystemItem itemLinkTo = _sourcePath.Resolved.ResolvedFsi;
                if( itemLinkTo is FileSysEmulator.FsLink )
                    throw new ApplicationException( "Can't create link to link." );

                FileSysEmulator.FileSystemItem targetContainer = _destPath.Resolved.ResolvedFsi;
                if( targetContainer is FileSysEmulator.FsLink )
                    targetContainer = fse.GetTargetItemOfLink( (FileSysEmulator.FsLink)targetContainer );

                if( targetContainer is FileSysEmulator.FsDir )
                {
                    FileSysEmulator.FsDir dir = (FileSysEmulator.FsDir)targetContainer;
                    dir.AddLink( itemLinkTo, false );
                }
                else if( targetContainer is FileSysEmulator.FsDrive )
                {
                    FileSysEmulator.FsDrive drv = (FileSysEmulator.FsDrive)targetContainer;
                    drv.AddLink( itemLinkTo, false );
                }
                else
                    throw new ApplicationException( String.Format("Unsupported link target '{0}'.", targetContainer.GetType().Name) );
            }
        }

        [FsCommand]
        public class CommandMdl: CommandBase
        {
            public const String NAME = "mdl";

            private ArgInfo _sourcePath, _destPath;

            public override String Name
            {
                get { return NAME; }
            }
            public override Int32 NumberArgsMin
            {
                get { return 1; }
            }
            public override Int32 NumberArgsMax
            {
                get { return 2; }
            }

            protected CommandMdl() {}

            protected override void Construct()
            {
                _sourcePath = _arguments[ 0 ];
                if( _arguments.Length == 2 )
                    _destPath = _arguments[ 1 ];
                else            
                    _destPath = ArgInfo.CurrDirOfCurrDrive;

                _destPath.PathTarget = ArgInfo.TargetType.Dir;
            }

            public override ArgInfo[] PathsMustExist
            {
                get { return new ArgInfo[]{ _sourcePath, _destPath }; }
            }

            public override void Execute( FileSysEmulator fse )
            {
                FileSysEmulator.FileSystemItem itemLinkTo = _sourcePath.Resolved.ResolvedFsi;
                if( itemLinkTo is FileSysEmulator.FsLink )
                    throw new ApplicationException( "Can't create link to link." );

                FileSysEmulator.FileSystemItem targetContainer = _destPath.Resolved.ResolvedFsi;
                if( targetContainer is FileSysEmulator.FsLink )
                    targetContainer = fse.GetTargetItemOfLink( (FileSysEmulator.FsLink)targetContainer );

                if( targetContainer is FileSysEmulator.FsDir )
                {
                    FileSysEmulator.FsDir dir = (FileSysEmulator.FsDir)targetContainer;
                    dir.AddLink( itemLinkTo, true );
                }
                else if( targetContainer is FileSysEmulator.FsDrive )
                {
                    FileSysEmulator.FsDrive drv = (FileSysEmulator.FsDrive)targetContainer;
                    drv.AddLink( itemLinkTo, true );
                }
                else
                    throw new ApplicationException( String.Format("Unsupported link target '{0}'.", targetContainer.GetType().Name) );
            }
        }

        [FsCommand]
        public class CommandDel: CommandBase
        {
            public const String NAME = "del";

            private ArgInfo _sourceFilePath;

            public override String Name
            {
                get { return NAME; }
            }
            public override Int32 NumberArgsMin
            {
                get { return 1; }
            }
            public override Int32 NumberArgsMax
            {
                get { return 1; }
            }

            protected CommandDel() {}

            protected override void Construct()
            {
                _sourceFilePath = _arguments[ 0 ];
                //_sourceFilePath.PathTarget = TargetType.File;
                //should be failed at Execute if target is a Dir
            }

            public override ArgInfo[] PathsMustExist
            {
                get { return new ArgInfo[]{ _sourceFilePath }; }
            }

            public override void Execute( FileSysEmulator fse )
            {
                FileSysEmulator.FileSystemItem item = _sourceFilePath.Resolved.ResolvedFsi;
                if( (item is FileSysEmulator.FsFile) || (item is FileSysEmulator.FsLink) )                    
                    fse.RemoveItem( item );
                else
                    throw new ApplicationException( String.Format("Command 'Del' can't remove '{0}'.", item.GetFullPath()) );
            }
        }

        [FsCommand]
        public class CommandCopy: CommandBase
        {
            public const String NAME = "copy";

            private ArgInfo _sourcePath, _destPath;

            public override String Name
            {
                get { return NAME; }
            }
            public override Int32 NumberArgsMin
            {
                get { return 1; }
            }
            public override Int32 NumberArgsMax
            {
                get { return 2; }
            }

            protected CommandCopy() {}

            protected override void Construct()
            {
                _sourcePath = _arguments[ 0 ];
                if( _arguments.Length == 2 )
                    _destPath = _arguments[ 1 ];
                else            
                    _destPath = ArgInfo.CurrDirOfCurrDrive;

                _destPath.PathTarget = ArgInfo.TargetType.Dir;
            }

            public override ArgInfo[] PathsMustExist
            {
                get { return new ArgInfo[]{ _sourcePath, _destPath }; }
            }

            public override void Execute( FileSysEmulator fse )
            {                
                FileSysEmulator.FileSystemItem srcItem = _sourcePath.Resolved.ResolvedFsi;
                if( (srcItem is FileSysEmulator.FsLink) || (srcItem is FileSysEmulator.FsDir) || (srcItem is FileSysEmulator.FsFile) )
                {
                    FileSysEmulator.FileSystemItem dst = _destPath.Resolved.ResolvedFsi;
                    if( dst is FileSysEmulator.FsLink )
                        dst = fse.GetTargetItemOfLink( (FileSysEmulator.FsLink)dst );
                    if( !(dst is FileSysEmulator.FsContainer) )
                        throw new ApplicationException( String.Format("Can't copy '{0}' to '{1}.'", srcItem.GetFullPath(), dst.GetFullPath()) );

                    FileSysEmulator.FsContainer parent = (FileSysEmulator.FsContainer)srcItem.Parent;
                    FileSysEmulator.FsContainer dstContainer = (FileSysEmulator.FsContainer)dst;
                    if( (FileSysEmulator.FsContainer)srcItem.Parent == dstContainer )
                        throw new ApplicationException( String.Format("Can't copy the '{0}' over itself.", srcItem.GetFullPath()) );

                    fse.StartCopying( srcItem );
                    parent.CopyItem( srcItem, dstContainer );
                    fse.EndCopying();
                }
                else
                    throw new ApplicationException( String.Format("Can't copy the '{0}'.", srcItem.GetFullPath()) );
            }
        }

        [FsCommand]
        public class CommandMove: CommandBase
        {
            public const String NAME = "move";
            private ArgInfo _sourcePath, _destPath;

            public override String Name
            {
                get { return NAME; }
            }
            public override Int32 NumberArgsMin
            {
                get { return 1; }
            }
            public override Int32 NumberArgsMax
            {
                get { return 2; }
            }

            protected CommandMove() {}

            protected override void Construct()
            {
                _sourcePath = _arguments[ 0 ];
                if( _arguments.Length == 2 )
                    _destPath = _arguments[ 1 ];
                else            
                    _destPath = ArgInfo.CurrDirOfCurrDrive;
            }

            public override ArgInfo[] PathsMustExist
            {
                get { return new ArgInfo[]{ _sourcePath }; }
            }

            public override void Execute( FileSysEmulator fse )
            {
                FileSysEmulator.FileSystemItem itemSrc = _sourcePath.Resolved.ResolvedFsi;

                if( itemSrc.HasHardLink )
                    throw new ApplicationException( String.Format("Can't move the '{0}' because it has hardlinks pointing to.", itemSrc.GetFullPath()) );

                FileSysEmulator.FsContainer parent = (FileSysEmulator.FsContainer)itemSrc.Parent;

                _destPath.PathTarget = ArgInfo.TargetType.Dir;
                _destPath.Resolve( fse );
                _destPath.Resolved.ResolvedFsi = fse.SearchResolvedPath( _destPath );

                if( (itemSrc is FileSysEmulator.FsDir) || (itemSrc is FileSysEmulator.FsLink) )
                {
                    if( _destPath.Resolved.ResolvedFsi == null )
                        throw new ApplicationException( String.Format("Can't move the '{0}' because destination '{1}' is not found.", itemSrc.GetFullPath(), _destPath.Resolved.Arg) );

                    FileSysEmulator.FsContainer dst = (FileSysEmulator.FsContainer)_destPath.Resolved.ResolvedFsi;
                    if( (itemSrc is FileSysEmulator.FsDir) && itemSrc == dst )
                        throw new ApplicationException( String.Format("Can't move the '{0}' over itself.", itemSrc.GetFullPath(), _destPath.Resolved.Arg) );
                    parent.MoveItem( itemSrc, dst );
                }
                else if( itemSrc is FileSysEmulator.FsFile )
                {
                    if( _destPath.Resolved.ResolvedFsi == null )
                    {
                        ArgInfo filename;
                        _destPath.SeparateLastStep( out filename, ArgInfo.TargetType.Dir, ArgInfo.TargetType.File );
                        _destPath.Resolve( fse );
                        _destPath.Resolved.ResolvedFsi = fse.SearchResolvedPath( _destPath );
                        if( _destPath.Resolved.ResolvedFsi == null )
                            throw new ApplicationException( String.Format("Can't move the '{0}' because destination '{1}' is not found.", itemSrc.GetFullPath(), _destPath.Resolved.Arg) );

                        itemSrc.RenameItem( filename.Arg );
                        FileSysEmulator.FsContainer dst = (FileSysEmulator.FsContainer)_destPath.Resolved.ResolvedFsi;
                        parent.MoveItem( itemSrc, dst );
                    }
                    else
                    {
                        FileSysEmulator.FsContainer dst = (FileSysEmulator.FsContainer)_destPath.Resolved.ResolvedFsi;
                        parent.MoveItem( itemSrc, dst );
                    }
                }
                else
                    throw new ApplicationException( String.Format("Can't move the '{0}'.", itemSrc.GetFullPath()) );
                
            }
        }
    }
}
