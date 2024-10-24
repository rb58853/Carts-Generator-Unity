using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization.Configuration;
using System.Security.Permissions;
using Config;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using Utils;

namespace CardsGenerator
{

    public class Field
    {
        public string fieldName { get; private set; }
        public string type { get; private set; }
        public dynamic value { get; private set; }

        public Field(string fieldName, string type, dynamic value)
        {
            this.fieldName = fieldName;
            this.type = type;
            this.value = value;
        }
        public bool setValue(string value)
        {
            if (type == "String")
            {
                this.value = value;
                return true;
            }

            if (type == "Int32")
            {
                this.value = int.Parse(value);
                return true;
            }
            if (type == "Int64")
            {
                this.value = long.Parse(value);
                return true;
            }
            //Los valores vienen siempre desde un string, actualmente solo se pueden agregar enteros, longs, o string, en otro caso habria que implementar codigo
            throw new NotImplementedException();
        }
    }

    public class ClassInfo
    {
        public string ClassName { get; private set; }
        public PropertyInfo[] Properties { get; private set; }
        public FieldInfo[] Fields { get; private set; }
        // public MethodInfo[] Methods { get; private set; }


        public ClassInfo(Type cardType)
        {
            this.ClassName = cardType.Name;
            this.Properties = cardType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            this.Fields = cardType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        }


    }

    public class DynamicGenerator
    {
        public byte[] image { get; private set; }
        public string BaseTypeName { get; private set; }
        public string ClassName { get; private set; }
        private Dictionary<string, Dictionary<string, dynamic>> properties;
        private Dictionary<string, string> temp = new Dictionary<string, string>();

        public DynamicGenerator(Type type, string ClassName)
        {
            this.properties = new Dictionary<string, Dictionary<string, dynamic>>();
            ClassInfo info = new ClassInfo(type);
            this.BaseTypeName = info.ClassName;
            this.ClassName = ClassName;
            this.image = null;

            foreach (PropertyInfo prop in info.Properties)
                InitProperty(prop);
            foreach (FieldInfo field in info.Fields)
                InitField(field);

        }


        private void InitProperty(PropertyInfo prop)
        {
            //TODO aqui deberia tenerse en cuenta si el tipado de la propiedad es estatico u otro, para eso necesito saber exactamente que es lo que se quiere lograr con la aplicacion
            string propertyName = prop.Name;
            string propertyType = prop.PropertyType.UnderlyingSystemType.Name;

            if (prop.SetMethod != null && prop.GetMethod != null)
            {
                //En el caso que sea null entonces no se puede asignar valor, luego no lo agregamos a las propiedades, solo se usara por herencia en el mejor caso.
                bool privateSet = prop.SetMethod.IsPrivate;
                bool privateGet = prop.GetMethod.IsPrivate;

                dynamic value = FieldUtils.DefaultValue(prop.PropertyType.UnderlyingSystemType, propertyName);

                Dictionary<string, dynamic> property = new Dictionary<string, dynamic>{
                {"field", new Field(fieldName: propertyName, type: propertyType, value:value)},
                {"isField", false},
                {"type", propertyType},
                {"privateSet", privateSet},
                {"privateGet", privateGet},
                {"isPublic", true},
                {"isStatic", false}

            };

                properties.Add(propertyName, property);
            }

            //Generar la propiedad, ya sea diccionario o de forma dinamica
        }
        private void InitField(FieldInfo field)
        {
            //TODO aqui deberia tenerse en cuenta si el tipado de la propiedad es estatico u otro, para eso necesito saber exactamente que es lo que se quiere lograr con la aplicacion
            string propertyName = Utils.FieldUtils.getBaseName(field.Name);
            string propertyType = field.FieldType.UnderlyingSystemType.Name;

            dynamic value = FieldUtils.DefaultValue(field.FieldType, propertyName);
            string attrs = string.Join(" ", field.Attributes.ToString().Split(",").Select(item => item.ToLower()));

            if (!this.properties.ContainsKey(propertyName))
            {
                Dictionary<string, dynamic> property = new Dictionary<string, dynamic>{
                {"field", new Field(fieldName: propertyName, type: propertyType, value:value)},
                {"isField", true},
                {"type", propertyType},
                {"isPublic", !field.IsPrivate},
                {"fieldAttributes",attrs},
                {"isStatic",field.IsStatic}
            };

                properties.Add(propertyName, property);
            }
        }

        public List<Field> Fields()
        {
            List<Field> fields = new List<Field>();
            foreach (string prop in this.properties.Keys)
                fields.Add(this.properties[prop]["field"]);


            return fields;
        }
        public bool SetProperty(string propName, dynamic value)
        {
            try
            {
                this.properties[propName]["value"] = value;
                return true;
            }
            catch { return false; }
        }

        public void setClassName(string ClassName)
        {
            this.ClassName = ClassName.Replace(" ", ""); ;
        }

        public void setImage(byte[] image)
        {
            // Asigna el nombre de la imagen
            this.image = image;
        }

        public Dictionary<string, dynamic> getField(string field)
        {
            return this.properties[field];
        }

        public string getImagesFolderPath()
        {
            return Path.Combine(Environment.CurrentDirectory, "images");
        }

        private string GenerateCsCode()
        {
            string namespace_ = $"using System;\nnamespace {CardsGenerationConfig.namespace_}\n";
            namespace_ += "{\n";
            string classHead = $"\tclass {this.ClassName} : {this.BaseTypeName}\n";
            string propertiesBody = "\t{\n";
            string invokeMethod = $"\t\tpublic {ClassName}(";
            string invokeArgs = "";
            string invokeBody = "\t\t{\n";

            foreach (string csvar in this.properties.Keys)
            {
                string type = properties[csvar]["type"];
                var value = properties[csvar]["field"].value;
                value = (value != null) ? value.ToString() : "null";

                if (type == "String")
                    value = $"\"{value}\"";

                if (!properties[csvar]["isStatic"])
                {
                    invokeArgs += $"{type} {csvar} = {value}, ";
                    invokeBody += $"\t\t\tthis.{csvar} = {csvar};\n";
                }

                if (!properties[csvar]["isField"])
                {
                    if (properties[csvar]["privateSet"])
                        //En caso que sea privada una propiedad entonces hay que crearla en el cuerpo de las propiedades, en caso contrario solo se hereda.
                        //TODO se esta asumiendo que todo lo que tiene private set son variables publicas
                        propertiesBody += $"\t\tpublic new {type} {csvar}" + " { get; private set; }\n";
                }
                else
                {
                    if (!properties[csvar]["isPublic"])
                    {
                        string attrs = properties[csvar]["fieldAttributes"];
                        propertiesBody += attrs.Length > 0 ? $"\t\t{attrs} new {type} {csvar};\n" : $"\t\tnew {type} {csvar};\n";
                    }

                }


            }
            if (properties.Keys.Count() >= 0)
                invokeArgs = invokeArgs.Substring(0, invokeArgs.Length - 2);
            invokeArgs += ")\n";
            invokeBody += "\t\t}\n";
            invokeMethod += invokeArgs + invokeBody;

            string code = namespace_ + classHead + propertiesBody + invokeMethod + "\t}\n}";
            Console.WriteLine(code);
            return code;
        }
        private void SaveImage()
        {
            string folderPath = Path.Combine(Config.CardsGenerationConfig.cardsPath, $"{this.BaseTypeName}s", this.ClassName);
            string path = Path.Combine(folderPath, "image.jpg");
            File.WriteAllBytes(path, this.image);
        }
        public void WriteFile()
        {
            SaveImage();
            string code = GenerateCsCode();
            string folderPath = Path.Combine(Config.CardsGenerationConfig.cardsPath, $"{this.BaseTypeName}s", this.ClassName);
            // string folderPath = Path.Combine(Environment.CurrentDirectory, $"{this.BaseTypeName}s", this.ClassName);
            Directory.CreateDirectory(folderPath);
            string filePath = Path.Combine(folderPath, $"{this.ClassName}.cs");
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine(code);
            }
        }
    }
}

