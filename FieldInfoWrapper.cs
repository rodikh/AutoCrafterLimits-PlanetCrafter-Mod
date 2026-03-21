using HarmonyLib;

namespace AutoCrafterLimits
{
    internal sealed class FieldInfoWrapper<TInstance, TField>
    {
        private readonly AccessTools.FieldRef<TInstance, TField> _fieldRef;

        public FieldInfoWrapper(string fieldName)
        {
            _fieldRef = AccessTools.FieldRefAccess<TInstance, TField>(fieldName);
        }

        public TField Get(TInstance instance)
        {
            return _fieldRef(instance);
        }
    }
}
