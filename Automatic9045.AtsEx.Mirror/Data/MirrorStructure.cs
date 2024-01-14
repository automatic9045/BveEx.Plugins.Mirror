using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Automatic9045.AtsEx.Mirror.Data
{
    [XmlRoot]
    public class MirrorStructure
    {
        [XmlAttribute]
        public string Key = null;

        [XmlAttribute]
        public string TextureFileName = null;

        [XmlAttribute]
        public int TextureWidth = 512;

        [XmlAttribute]
        public int TextureHeight = 512;

        [XmlAttribute]
        public float Zoom = 1;
    }
}
