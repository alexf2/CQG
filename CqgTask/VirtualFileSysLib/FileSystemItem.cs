using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using System.Xml;

namespace CQG.VirtualFileSys
{    
   public sealed partial class FileSysEmulator
   { 
       /// <summary>
       /// Контекст, используемый для постоения дерева при печати структуры файловой системы в консоли.
       /// </summary>       
       public sealed class PrintCtx
       {
           public Int32 Level = 0;
           public List<Int32> Tabs = new List<Int32>();
           public List<Boolean> IsTheLast = new List<Boolean>();
       }

       /// <summary>
       /// Представляет базовый класс для всех элементов файловой системы.
       /// </summary>
       /// <remarks>
       /// Отвечает за получение уникального ID каждым элементом, хранит владельцев элемента (файловый менеджер
       /// и родителя), список всех линков, указывающих на элемент.
       /// </remarks>
       public abstract class FileSystemItem
        {
           #region Private fields
            private Int64 _id;
            private String _name, _serachedName;
            private FileSysEmulator _owner;
            private FileSystemItem _parent;

            private List<FsLink> _links = new List<FsLink>();
           #endregion

           #region Constructors
           public FileSystemItem( String name )
            {
                _id = FileSysEmulator.GetNextId();             
                AssignName( name );
            }
           #endregion

           #region Cloning
            protected internal abstract FileSystemItem Clone();
            protected virtual void InternalClonemembers( FileSystemItem src )
            {
                //base.InternalClonemembers();
                _id = FileSysEmulator.GetNextId();
                src.Owner._copyingMapOfIDs.Add( src.ID, _id );
                _parent = null;
                _links = new List<FsLink>();
            }
           #endregion

           #region Static
           static public Boolean BelongsTo( FileSystemItem parent, FileSystemItem child, Boolean walkup )
           {
               if( !walkup )
                return parent == child.Parent;
               else
               {
                   Boolean found = false;
                   for( FileSystemItem p = child.Parent; p != null; p = p.Parent )
                       if( p == parent )
                       {
                           found = true; break;
                       }
                   return found;
               }
           }
           #endregion

           #region Properties
           public FileSysEmulator Owner
            {
                get { return _owner; }
                protected set { _owner = value; }
            }
           public FileSystemItem Parent
            {
                get { return _parent; }
                protected set { _parent = value; }
            }

            public Int64 ID
            {
                get { return _id; }
            }
            public String Name
            {
                get { return _name; }
            }
            public String SearchedName
            {
                get { return _serachedName; }
            }

            public virtual String DisplayName
            {
                get { return Name; }
            }

            public virtual String XmlTag
            {
                get { return GetType().Name; }
            }

           public Boolean HasHardLink
            {
                get
                {
                    Boolean res = false;
                    foreach( FsLink lnk in _links )
                        if( !lnk.IsDynamic )
                        {
                            res = true; break;
                        }
                    return res;
                }
            }
           #endregion

           #region Methods
           protected void AssignName( String name )
           {
                _name = name;
                _serachedName = GetSearchedName( name );
           }
           protected virtual String GetSearchedName( String name )
           {
               return name.ToLower();
           }

           public FsDrive GetParentDrive()
           {
               FileSystemItem item = this;
               while( item != null && !(item is FsDrive) )
                   item = item.Parent;
               return (FsDrive)item;
           }
            
            public virtual XmlElement GetAsXmlElement( XmlDocument parentDoc )
            {
                XmlElement res = parentDoc.CreateElement( XmlTag );
                SetAttributesOnTag( res );            
                return res;
            }
            public virtual void SetAttributesOnTag( XmlElement el )
            {
                el.SetAttribute( "id", XmlConvert.ToString(_id) );
                el.SetAttribute( "name", _name );
            }

           

           public List<String> GetLocationSteps( Boolean getSearched )
           {
               List<String> res = new List<String>();
               for( FileSystemItem p = this; p != null && !(p is FsDrive) && !(p is FsRoot); p = p.Parent )
                   res.Add( getSearched ? p.SearchedName:p.Name );

               res.Reverse();
               return res;
           }

           public String GetFullPath()
           {
               StringBuilder bld = new StringBuilder();

               List<String> lst = new List<String>();
               for( FileSystemItem p = this; p != null && !(p is FsRoot); p = p.Parent )
                   lst.Add( p.Name );

               lst.Reverse();

               bld.Append( lst[0] + ":\\" );
               for( Int32 i = 1; i < lst.Count; ++i )
               {
                   bld.Append( lst[i] );
                   if( i != lst.Count - 1 )
                       bld.Append( "\\" );
               }
               return bld.ToString();
           }

           public void RegisterLink( FsLink lnk )
           {
               _links.Add( lnk ); 
           }
           public void UnregisterLink( FsLink lnk )
           {
               _links.Remove( lnk ); 
           }
           public List<FsLink> GetLinks()
           {
               return _links;
           }           

           protected internal void RenameItem( String newName )
           {
               if( HasHardLink )
                   throw new ApplicationException( String.Format("Can't rename '{0}'. There are hard links.", GetFullPath()) );
               if( (this is FsDir) || (this is FsFile) )
               {
                   AssignName( newName );
                   foreach( FsLink lnk in _links )
                       lnk.AssignName( newName );
               }
               else
                   throw new ApplicationException( String.Format("Can't rename '{0}'. Only files and dirs can be renamed.", GetFullPath()) );
           }

           #region Printing
           protected void PrintTabs( TextWriter wr, PrintCtx ctx, Boolean isLast )
           {
               for( Int32 i = 0 ; i < ctx.Tabs.Count; ++i )
               {
                   String spc = new String( ' ', ctx.Tabs[i] );
                   wr.Write( spc );
                   wr.Write( ctx.IsTheLast[i] ? ' ':'|' );
               }
               if( ctx.Level > 0 )
                   wr.Write( '_' );
           }
           public virtual void PrintTo( TextWriter wr, PrintCtx ctx, Boolean isLast )
           {
               PrintTabs( wr, ctx, isLast );
               String prt = Name;
               if( this is FsDrive )
                   prt += ":";
               wr.WriteLine( prt );
           }
           #endregion

           #endregion                       
        }
   }
}