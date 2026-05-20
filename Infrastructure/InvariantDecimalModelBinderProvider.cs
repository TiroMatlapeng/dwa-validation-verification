using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace dwa_ver_val.Infrastructure;

/// <summary>
/// BUG-001: Registers <see cref="InvariantDecimalModelBinder"/> for decimal and decimal?
/// model types. Must be inserted at index 0 in <c>options.ModelBinderProviders</c> so it
/// runs before the default <see cref="SimpleTypeModelBinderProvider"/>.
/// </summary>
public class InvariantDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var type = context.Metadata.ModelType;
        if (type == typeof(decimal) || type == typeof(decimal?))
        {
            var fallback = new SimpleTypeModelBinder(type, context.Services.GetRequiredService<ILoggerFactory>());
            return new InvariantDecimalModelBinder(fallback);
        }
        return null;
    }
}
