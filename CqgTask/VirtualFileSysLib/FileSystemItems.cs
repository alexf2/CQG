using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using System.Xml;

namespace CQG.VirtualFileSys
{    
   public sealed partial class FileSysEmulator
   {
        public class FsRoot: FsContainer
        {
            private FsDrive _currentDrive;

            protected internal override FileSystemItem Clone()
            {
                FsRoot res = (FsRoot)MemberwiseClone();
                res.InternalClonemembers( this );
                return res;
            }
            protected override void InternalClonemembers( FileSystemItem src )
            {
                base.InternalClonemembers( src );
                FsRoot s = (FsRoot)src;
                _currentDrive = this[ s.CurrentDrive.SearchedName ];
            }

            public FsRoot( FileSysEmulator owner ): base( "The file system root" )
            {
                //AssignName( Name );
                Owner = owner;
            }

            public override string ToString()
            {
                return "File system root";
            }
            public override string DisplayName
            {
                get { return "Root"; }
            }

            public FsDrive this[ Int64 id ]
            {
                get { return (FsDrive)base[ id ]; }
            }
            public FsDrive this[ String name ]
            {
                get { return (FsDrive)base[ name ]; }
            }
            
            public override void SetAttributesOnTag( XmlElement el )
            {
            }

            public FsDrive AddDrive( String letter )
            {
                FsDrive res = new FsDrive( letter );
                AddItem( res, false );
                if( _currentDrive == null )
                    _currentDrive = res;
                return res;
            }

            public FsDrive CurrentDrive
            {
                get { return _currentDrive; }
                set
                {
                    if( value.Owner != Owner )
                        throw new ApplicationException( String.Format("Can't set '{0}' as current. The drive object '{1:d}' belongs to another file system.", value.ToString(), value.ID) );

                    _currentDrive = value;
                }
            }

            public override void PrintTo( TextWriter wr, PrintCtx ctx, Boolean isLast )
            {
               foreach( KeyValuePair<Int64, FileSystemItem> kv in GetItems() )
                   kv.Value.PrintTo( wr, ctx, isLast );
            }

        }

        public class FsDrive: FsContainer
        {
            private FsDir _currentDir;

            public FsDrive( String letter ): base( letter )
            {
                //AssignName( letter );
            }

            protected internal override FileSystemItem Clone()
            {
                FsDrive res = (FsDrive)MemberwiseClone();
                res.InternalClonemembers( this );
                return res;
            }
            protected override void InternalClonemembers( FileSystemItem src )
            {
                base.InternalClonemembers( src );
                FsDrive s = (FsDrive)src;
                if( s.CurrentDir == null )
                    _currentDir = null;
                else
                    _currentDir = (FsDir)this[ s.CurrentDir.SearchedName ];
            }

            public override string ToString()
            {
                return String.Format( "Drive '{0}:'", DisplayName );
            }

            public FsDir AddDir( String name )
            {
                FsDir res = new FsDir( name );
                AddItem( res, false );
                return res;
            }
            public FsFile AddFile( String name )
            {
                FsFile res = new FsFile( name );
                AddItem( res, false );
                return res;
            }

            public FsLink AddLink( FileSystemItem itemLinkTo, Boolean isDynamic )
            {
                if( itemLinkTo is FsLink )
                    throw new ApplicationException( "Creating link to link is prohibited." );
                FsLink res = isDynamic ? (FsLink)new FsDynamicLink(itemLinkTo):(FsLink)new FsHardLink(itemLinkTo);
                itemLinkTo.RegisterLink( res );
                AddItem( res, true );
                return res;
            }

            public FsDir CurrentDir
            {
                get { return _currentDir; }
                set
                {
                    if( value != null )
                    {
                        if( value.Owner != Owner )
                            throw new ApplicationException( String.Format("Can't set '{0}' as current. The directory object '{1:d}' belongs to another file system.", value.ToString(), value.ID) );

                        if( !BelongsTo(this, value, true) )
                            throw new ApplicationException( String.Format("Can't set '{0}' as current. The directory object '{1:d}' belongs to another drive.", value.ToString(), value.ID) );
                    }

                    _currentDir = value;
                }
            }
            
        }

        public class FsDir: FsContainer
        {
            public FsDir( String name ): base( name )
            {                
            }

            protected internal override FileSystemItem Clone()
            {
                FsDir res = (FsDir)MemberwiseClone();
                res.InternalClonemembers( this );
                return res;
            }            

            public override string ToString()
            {
                return String.Format( "Directory '{0}'", Name );
            }

            public FsDir AddDir( String name )
            {
                FsDir res = new FsDir( name );
                AddItem( res, false );
                return res;
            }
            public FsFile AddFile( String name )
            {
                FsFile res = new FsFile( name );
                AddItem( res, false );
                return res;
            }

            protected override String GetSearchedName( String name )
            {
               return base.GetSearchedName(name) + "_d";
            }

            public FsLink AddLink( FileSystemItem itemLinkTo, Boolean isDynamic )
            {
                if( itemLinkTo is FsLink )
                    throw new ApplicationException( "Creating link to link is prohibited." );
                FsLink res = isDynamic ? (FsLink)new FsDynamicLink(itemLinkTo):(FsLink)new FsHardLink(itemLinkTo);
                itemLinkTo.RegisterLink( res );
                AddItem( res, true );
                return res;
            }
            
        }

        public class FsFile: FileSystemItem
        {
            public FsFile( String name ): base( name )
            {
                //AssignName( name );
            }

            protected internal override FileSystemItem Clone()
            {
                FsFile res = (FsFile)MemberwiseClone();
                res.InternalClonemembers( this );
                return res;
            }
            

            public override string ToString()
            {
                return String.Format( "File '{0}'", Name );
            }                    
        }    
        
    }
}
