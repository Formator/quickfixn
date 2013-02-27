using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuickFix.Fields
{
    public class DateTimeField : FieldBase<DateTime>
    {
        protected readonly bool showMilliseconds = true;
        protected readonly bool _isRelativeTime = false;

        public DateTimeField(int tag)
            : base(tag, new DateTime()) {}

        public DateTimeField(int tag, DateTime dt)
            : base(tag, dt) {}

        public DateTimeField(int tag, DateTime dt, bool showMilliseconds)
            : base(tag, dt) { this.showMilliseconds = showMilliseconds; }

        public DateTimeField(int tag, DateTime dt, bool showMilliseconds, bool relativeTime)
            : base(tag, dt) {this.showMilliseconds = showMilliseconds; this._isRelativeTime = relativeTime; }

        // quickfix compat
        public DateTime getValue()
        { return Obj; }

        public void setValue(DateTime dt)
        { Obj = dt; }

        protected override string makeString()
        {
            return !_isRelativeTime ? Converters.DateTimeConverter.Convert(Obj, showMilliseconds) : Converters.DateTimeConverter.ConvertRelative(Obj, showMilliseconds);
        }
    }

    public class DateOnlyField : DateTimeField
    {
        public DateOnlyField(int tag)
            : base(tag, new DateTime()) { }

        public DateOnlyField(int tag, DateTime dt)
            : base(tag, dt) { }

        public DateOnlyField(int tag, DateTime dt, bool showMilliseconds)
            : base(tag, dt, showMilliseconds) { }

        public DateOnlyField(int tag, DateTime dt, bool showMilliseconds, bool relativeTime)
            : base(tag, dt, showMilliseconds, relativeTime) { }

        protected override string makeString()
        {
            return !_isRelativeTime ? Converters.DateTimeConverter.ConvertDateOnly(Obj) : Converters.DateTimeConverter.ConvertRelativeDateOnly(Obj);
        }
    }

    public class TimeOnlyField : DateTimeField
    {
        public TimeOnlyField(int tag)
            : base(tag, new DateTime()) { }

        public TimeOnlyField(int tag, DateTime dt)
            : base(tag, dt) { }

        public TimeOnlyField(int tag, DateTime dt, bool showMilliseconds)
            : base(tag, dt, showMilliseconds) { }

        public TimeOnlyField(int tag, DateTime dt, bool showMilliseconds, bool relativeTime)
            : base(tag, dt, showMilliseconds, relativeTime) { }
        protected override string makeString()
        {
            return Converters.DateTimeConverter.ConvertTimeOnly(Obj, base.showMilliseconds); 
        }
    }
}
