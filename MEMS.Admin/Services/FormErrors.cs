using MEMS.Data.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace MEMS.Admin.Services;

// Shows service-layer validation errors (e.g. "slug already used") next to the matching form field,
// alongside the DataAnnotations messages. Errors for unknown fields go to the validation summary.
public sealed class FormErrors
{
    private readonly EditContext _editContext;
    private readonly ValidationMessageStore _store;

    public FormErrors(EditContext editContext)
    {
        _editContext = editContext;
        _store = new ValidationMessageStore(editContext);
        // Server messages disappear as soon as the user edits the form again.
        editContext.OnFieldChanged += (_, args) =>
        {
            _store.Clear(args.FieldIdentifier);
            _store.Clear(new FieldIdentifier(editContext.Model, string.Empty));
        };
        editContext.OnValidationRequested += (_, _) => _store.Clear();
    }

    // Returns the message to show at page level (Forbidden / NotFound), or null.
    public string? Apply(ServiceResult result)
    {
        _store.Clear();
        if (result.Status != ServiceStatus.Invalid)
        {
            return result.Message;
        }

        var model = _editContext.Model;
        foreach (var (field, messages) in result.Errors)
        {
            var hasProperty = field.Length > 0 && model.GetType().GetProperty(field) is not null;
            _store.Add(new FieldIdentifier(model, hasProperty ? field : string.Empty), messages);
        }
        _editContext.NotifyValidationStateChanged();
        return null;
    }

    public void AddError(string message)
    {
        _store.Add(new FieldIdentifier(_editContext.Model, string.Empty), message);
        _editContext.NotifyValidationStateChanged();
    }
}
