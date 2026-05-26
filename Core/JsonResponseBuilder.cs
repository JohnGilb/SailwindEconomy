using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EconomyRevamp
{
    internal sealed class JsonResponseBuilder
    {
        private struct Context
        {
            public bool First;
            public bool IsObject;
        }

        private readonly StringBuilder _builder = new StringBuilder(64 * 1024);
        private readonly List<Context> _contexts = new List<Context>();
        private bool _afterPropertyName;

        public override string ToString()
        {
            return _builder.ToString();
        }

        public void BeginObject()
        {
            BeforeValue();
            _builder.Append('{');
            _contexts.Add(new Context { First = true, IsObject = true });
        }

        public void EndObject()
        {
            _builder.Append('}');
            _contexts.RemoveAt(_contexts.Count - 1);
        }

        public void BeginArray()
        {
            BeforeValue();
            _builder.Append('[');
            _contexts.Add(new Context { First = true, IsObject = false });
        }

        public void EndArray()
        {
            _builder.Append(']');
            _contexts.RemoveAt(_contexts.Count - 1);
        }

        public void PropertyName(string name)
        {
            BeforeProperty();
            WriteEscapedString(name);
            _builder.Append(':');
            _afterPropertyName = true;
        }

        public void BeginObjectProperty(string name)
        {
            PropertyName(name);
            BeginObject();
        }

        public void BeginArrayProperty(string name)
        {
            PropertyName(name);
            BeginArray();
        }

        public void Property(string name, string value)
        {
            PropertyName(name);
            Value(value);
        }

        public void Property(string name, int value)
        {
            PropertyName(name);
            Value(value);
        }

        public void Property(string name, float value)
        {
            PropertyName(name);
            Value(value);
        }

        public void Property(string name, double value)
        {
            PropertyName(name);
            Value(value);
        }

        public void Property(string name, bool value)
        {
            PropertyName(name);
            Value(value);
        }

        public void NullProperty(string name)
        {
            PropertyName(name);
            Null();
        }

        public void Value(string value)
        {
            BeforeValue();
            if (value == null)
            {
                _builder.Append("null");
                return;
            }

            WriteEscapedString(value);
        }

        public void Value(int value)
        {
            BeforeValue();
            _builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Value(float value)
        {
            BeforeValue();
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                _builder.Append("null");
                return;
            }

            _builder.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public void Value(double value)
        {
            BeforeValue();
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                _builder.Append("null");
                return;
            }

            _builder.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public void Value(bool value)
        {
            BeforeValue();
            _builder.Append(value ? "true" : "false");
        }

        public void Null()
        {
            BeforeValue();
            _builder.Append("null");
        }

        private void BeforeValue()
        {
            if (_afterPropertyName)
            {
                _afterPropertyName = false;
                return;
            }

            if (_contexts.Count == 0)
            {
                return;
            }

            int index = _contexts.Count - 1;
            Context context = _contexts[index];
            if (!context.First)
            {
                _builder.Append(',');
            }

            context.First = false;
            _contexts[index] = context;
        }

        private void BeforeProperty()
        {
            if (_contexts.Count == 0)
            {
                return;
            }

            int index = _contexts.Count - 1;
            Context context = _contexts[index];
            if (!context.IsObject)
            {
                throw new InvalidOperationException("JSON property written outside an object.");
            }

            if (!context.First)
            {
                _builder.Append(',');
            }

            context.First = false;
            _contexts[index] = context;
        }

        private void WriteEscapedString(string value)
        {
            _builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        _builder.Append("\\\\");
                        break;
                    case '"':
                        _builder.Append("\\\"");
                        break;
                    case '\b':
                        _builder.Append("\\b");
                        break;
                    case '\f':
                        _builder.Append("\\f");
                        break;
                    case '\n':
                        _builder.Append("\\n");
                        break;
                    case '\r':
                        _builder.Append("\\r");
                        break;
                    case '\t':
                        _builder.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                        {
                            _builder.Append("\\u");
                            _builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _builder.Append(c);
                        }
                        break;
                }
            }
            _builder.Append('"');
        }
    }
}
