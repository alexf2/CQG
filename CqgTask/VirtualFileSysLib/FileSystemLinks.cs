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
	   /// Базовый класс для жёстких и динамических линков.
	   /// </summary>
	   /// <remarks>
       /// При клонировании региструет свою копию в целевом элементе файловой системы.
       /// Если этот линк указывает внутрь скопированного поддерева, то после окончания копирования
       /// всего поддерева надо вызвать метод FlipLinkTo, чтобы привязать связь к скопированному элементу.
       /// </remarks>
       public abstract class FsLink: FileSystemItem
        {
            private FileSystemItem _itemTo;
            public FsLink( FileSystemItem itemTo ): base( itemTo.Name )
            {                
                _itemTo = itemTo;
                AssignName( Name );
            }
            public abstract Boolean IsDynamic
            {
                get;
            }
            
            protected override void InternalClonemembers( FileSystemItem src )
            {
                base.InternalClonemembers( src );
                FsLink s = (FsLink)src;
                if( _itemTo != null )
                    _itemTo.RegisterLink( this );
            }

            protected internal void FlipLinkTo( FileSystemItem itemToNew )
            {
                _itemTo.UnregisterLink( this );
                itemToNew.RegisterLink( this );
                _itemTo = itemToNew;
                AssignName( itemToNew.Name );
            }

            public FileSystemItem ItemTo
            {
                get { return _itemTo; }
            }

            protected override String GetSearchedName( String name )
            {
               String res = base.GetSearchedName( name );
               if( _itemTo is FsDir )
                   res += "_d";
               return res;
            }

            public override void SetAttributesOnTag( XmlElement el )
            {
                base.SetAttributesOnTag( el );
                el.SetAttribute( "target-id", XmlConvert.ToString(_itemTo.ID) );
            }

            public override void PrintTo( TextWriter wr, PrintCtx ctx, Boolean isLast )
            {
                PrintTabs( wr, ctx, isLast );
                wr.WriteLine( String.Format("{0}[{1}]", IsDynamic ? "dlink":"hlink", _itemTo.GetFullPath()) );
            }
        }

        public class FsHardLink: FsLink
        {
            protected internal override FileSystemItem Clone()
            {
                FsHardLink res = (FsHardLink)MemberwiseClone();
                res.InternalClonemembers( this );
                return res;
            }

            public FsHardLink( FileSystemItem itemTo ): base( itemTo )
            {            
                //AssignName( Name );
            }
            public override Boolean IsDynamic
            {
                get { return false; }
            }
        }
        public class FsDynamicLink: FsLink
        {
            protected internal override FileSystemItem Clone()
            {
                FsDynamicLink res = (FsDynamicLink)MemberwiseClone();
                res.InternalClonemembers( this );
                return res;
            }

            public FsDynamicLink( FileSystemItem itemTo ): base( itemTo )
            {
                //AssignName( Name );
            }
            public override Boolean IsDynamic
            {
                get { return true; }
            }
        }    
   }
}