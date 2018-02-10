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
       /// Базовый класс для всех контейнеров файловой системы: корня, диска, фолдера.
       /// </summary>
       /// <remarks>
       /// Реализует основную часть функциональности по копированию, перемещению, удалению
       /// и переименованию элементов.
       /// 
       /// При клонировании поддеревьев проверяет типы встречающихся линков.
       /// Линки, которые смотрят внутрь клонируемого дерева передаются файловому менеджеру
       /// для последующего флиппинга, который происходит по окончанию копирования.
       /// </remarks>
       public abstract class FsContainer: FileSystemItem
        {
            private Dictionary<Int64, FileSystemItem> _itemsById = new Dictionary<Int64, FileSystemItem>();
            private Dictionary<String, FileSystemItem> _itemsByName = new Dictionary<String, FileSystemItem>();

            public FsContainer( String name ): base( name )
            {            
            }

            protected override void InternalClonemembers( FileSystemItem src )
            {
                base.InternalClonemembers( src );
                FsContainer s = (FsContainer)src;
                _itemsById = new Dictionary<Int64, FileSystemItem>();
                _itemsByName = new Dictionary<String, FileSystemItem>();

                foreach( KeyValuePair<Int64, FileSystemItem> kv in s._itemsById )
                {
                    FileSystemItem srcItem = kv.Value;
                    if( srcItem is FsLink )
                    {
                        FsLink lnk = (FsLink)srcItem;
                        FsLink lnkNew = (FsLink)srcItem.Clone();
                        AddItem( lnkNew, false );
                        if( FileSystemItem.BelongsTo(src.Owner._currentMovingRoot, lnk.ItemTo, true) )
                            src.Owner._linksToFlip.Add( lnkNew );
                    }
                    else
                        AddItem( srcItem.Clone(), false );
                }
            }

            public Boolean IsEmpty
            {
                get { return _itemsById.Count == 0; }
            }

           protected Dictionary<Int64, FileSystemItem> GetItems()
           {
               return _itemsById;
           }

           protected internal void CopyItem( FileSystemItem item, FsContainer parent )
           {
               if( item.Parent != this )
                   throw new ApplicationException( String.Format("Illegal call to CopyItem: '{0}' is owned by another container.", item.GetFullPath()) );

               if( parent._itemsById.ContainsKey(item.ID) || parent._itemsByName.ContainsKey(item.SearchedName) )
                   throw new ApplicationException( String.Format("Can't copy '{0}' to '{1}': there is an duplicated item.", item.GetFullPath(), parent.GetFullPath()) );

               item = item.Clone();

               parent.AddItem( item, false );
           }

           protected internal void MoveItem( FileSystemItem item, FsContainer parent )
           { 
               if( item.Parent != this )
                   throw new ApplicationException( String.Format("Illegal call to MoveItem: '{0}' is owned by another container.", item.GetFullPath()) );

               if( parent._itemsById.ContainsKey(item.ID) || parent._itemsByName.ContainsKey(item.SearchedName) )
                   throw new ApplicationException( String.Format("Can't move '{0}' to '{1}': there is an duplicated item.", item.GetFullPath(), parent.GetFullPath()) );

               _itemsById.Remove( item.ID );
               _itemsByName.Remove( item.SearchedName );
               Owner.UnregisterItem( item );
               item.Parent = null;

               parent.AddItem( item, false );
           }

            protected internal void RemoveItem( FileSystemItem item )
            {
                if( item.HasHardLink )
                    throw new ApplicationException( String.Format("Can't remove '{0}' because there are hard links pointing to.", item.GetFullPath()) );

                foreach( FsLink lnk in item.GetLinks() )
                    ((FsContainer)lnk.Parent).RemoveItem( lnk );
                

                if( item is FsContainer )
                {
                    List<FileSystemItem> lstToRemove = new List<FileSystemItem>();
                    foreach( KeyValuePair<Int64, FileSystemItem> kv in ((FsContainer)item)._itemsById )
                        lstToRemove.Add( kv.Value );

                    foreach( FileSystemItem it in lstToRemove )
                        RemoveItem( it );
                }

                _itemsById.Remove( item.ID );
                _itemsByName.Remove( item.SearchedName );
                Owner.UnregisterItem( item );
                item.Owner = null;
                item.Parent = null;
            }

            protected internal void AddItem( FileSystemItem item, Boolean noThrowException )
            {
                if( _itemsById.ContainsKey(item.ID) )
                    throw new ApplicationException( String.Format("Can't add '{0}' into '{1}': item with the same ID already exists.", item.ToString(), ToString()) );

                if( _itemsByName.ContainsKey(item.SearchedName) )
                {
                    if( noThrowException )
                        return;
                    else
                        throw new ApplicationException( String.Format("Can't add '{0}' into '{1}': item with the same name already exists.", item.ToString(), ToString()) );
                }

                _itemsById.Add( item.ID, item );
                _itemsByName.Add( item.SearchedName, item );
                
                item.Parent = this;
                item.Owner = this.Owner;
                Owner.RegisterItem( item );
            }

            public FileSystemItem this[ Int64 id ]
            {
                get { return _itemsById[ id ]; }
            }
            public FileSystemItem this[ String name ]
            {
                get 
                { 
                    FileSystemItem res;
                    _itemsByName.TryGetValue( name, out res );
                    return res;
                }
            }


            public XmlDocument GetAsXml()
            {
                XmlDocument doc = new XmlDocument();
                doc.AppendChild( GetAsXmlElement(doc) );
                return doc;
            }

            public override XmlElement GetAsXmlElement( XmlDocument parentDoc )
            {
                XmlElement res = base.GetAsXmlElement( parentDoc );
                
                foreach( KeyValuePair<Int64, FileSystemItem> kv in _itemsById )
                    res.AppendChild( kv.Value.GetAsXmlElement(parentDoc) );

                return res;
            }

           protected enum Sorting
           {
               ByName_Asc
           }
           private SortedList<String, FileSystemItem> GetChildsView( Sorting srtType )
           {
               SortedList<String, FileSystemItem> res = new SortedList<String, FileSystemItem>();
               switch( srtType )
               {
                   case Sorting.ByName_Asc:
                   {
                       foreach( KeyValuePair<Int64, FileSystemItem> kv in _itemsById )
                           res.Add( kv.Value.Name, kv.Value );
                   }
                   break;

                   default:
                       throw new ApplicationException( String.Format("Unimplemented sorting: '{0}'.", srtType.ToString()) );
               }
               return res;
           }


           public override void PrintTo( TextWriter wr, PrintCtx ctx, Boolean isLast )
            {
               base.PrintTo( wr, ctx, isLast );

               ctx.Level += 1;
               ctx.Tabs.Add( Name.Length - 1 );
               if( ctx.IsTheLast.Count > 0 )
                    ctx.IsTheLast[ ctx.IsTheLast.Count - 1 ] = isLast;
               ctx.IsTheLast.Add( false );

               SortedList<String, FileSystemItem> lst = GetChildsView( Sorting.ByName_Asc );
               foreach( KeyValuePair<String, FileSystemItem> kv in lst )
                   kv.Value.PrintTo( wr, ctx, lst.IndexOfKey(kv.Key) == lst.Count - 1 );

               ctx.IsTheLast.RemoveAt( ctx.IsTheLast.Count - 1 );
               ctx.Tabs.RemoveAt( ctx.Tabs.Count - 1 );
               ctx.Level -= 1;
            }
        }
   }
}
