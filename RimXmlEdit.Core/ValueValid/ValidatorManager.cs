using System.ComponentModel.DataAnnotations;

namespace RimXmlEdit.Core.ValueValid;

public class ValidatorManager
{
    private List<IValueValidator> _validators;

    public IEnumerable<IValueValidator> ValueValids => _validators;

    public ValidatorManager()
    {
        _validators =
        [
            new UnityEntryValidator(),
            new SimpleInstanceValidator(),
            new EnumableValidator(),
            new PathValidator(),
            new PrimitiveValidator(),
        ];
    }

    public void RegisterValidator(IValueValidator validator)
        => _validators.Add(validator);
}
