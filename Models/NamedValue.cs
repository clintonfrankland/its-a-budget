using System;

namespace ClintonFrankland
{
    public class NamedValue
    {

        private int _intId = -1;
        private string _strName = "";
        private object _objValue;
        private bool _bolIsBinary = false;

        public NamedValue()
        {
            _objValue = "";
            ValueUpdated?.Invoke(_strName, _objValue);
        }

        public NamedValue(string name)
        {
            _strName = name;
            _objValue = "";
            ValueUpdated?.Invoke(_strName, _objValue);
        }

        public NamedValue(string name, object value)
        {
            _strName = name;
            _objValue = value;
            ValueUpdated?.Invoke(_strName, _objValue);
        }

        public NamedValue(int id, string name, object value)
        {
            _intId = id;
            _strName = name;
            _objValue = value;
            ValueUpdated?.Invoke(_strName, _objValue);
        }

        public NamedValue(string name, object value, bool isbinary)
        {
            _strName = name;
            _objValue = value;
            _bolIsBinary = isbinary;
            ValueUpdated?.Invoke(_strName, _objValue);
        }

        public int Id
        {
            get
            {
                return _intId;
            }
            set
            {
                _intId = value;
            }
        }

        public string Name
        {
            get
            {
                return _strName;
            }
            set
            {
                _strName = value;
            }
        }

        public object Value
        {
            get
            {
                return _objValue;
            }
            set
            {
                _objValue = value;
                ValueUpdated?.Invoke(_strName, _objValue);
            }
        }

        public string ValueString
        {
            get
            {
                return Convert.ToString(_objValue);
            }
        }

        public int ValueInteger
        {
            get
            {
                return Convert.ToInt32(_objValue);
            }
        }

        public bool ValueBoolean
        {
            get
            {
                string strValue = _objValue.ToString().ToLower();
                if (strValue == "true" | strValue == "yes" | strValue == "1")
                {
                    return true;
                }
                else if (strValue == "false" | strValue == "no" | strValue == "0")
                {
                    return false;
                }
                else
                {
                    throw new InvalidOperationException("Property is not a valid boolean value.");
                }
            }
        }

        public bool IsBinary
        {
            get
            {
                return _bolIsBinary;
            }
            set
            {
                _bolIsBinary = value;
            }
        }

        public bool IsInteger
        {
            get
            {
                try
                {
                    int intTemp = int.Parse(_objValue.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
        }

        public bool IsBoolean
        {
            get
            {
                string strValue = _objValue.ToString().ToLower();
                if (strValue == "true" | strValue == "yes" | strValue == "false" | strValue == "no" | strValue == "1" | strValue == "0")
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public event ValueUpdatedEventHandler ValueUpdated;

        public delegate void ValueUpdatedEventHandler(string Key, object Value);

    }
}