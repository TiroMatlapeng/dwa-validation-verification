using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace dwa_ver_val.Infrastructure;

/// <summary>
/// BUG-001: Custom decimal model binder that always parses with InvariantCulture
/// and accepts both dot- and comma-decimal input. HTML <input type="number"> always
/// submits dot-decimal values, but the default DecimalModelBinder resolves its
/// culture from CultureInfo.CurrentCulture (process culture, set from the OS),
/// not from ASP.NET Core's RequestLocalization middleware. On hosts with
/// comma-decimal cultures (e.g. en_ZA) this rejects every "10.00" POST.
/// </summary>
public class InvariantDecimalModelBinder : IModelBinder
{
    private readonly IModelBinder _fallback;

    public InvariantDecimalModelBinder(IModelBinder fallback) => _fallback = fallback;

    public Task BindModelAsync(ModelBindingContext ctx)
    {
        var valueResult = ctx.ValueProvider.GetValue(ctx.ModelName);
        if (valueResult == ValueProviderResult.None)
            return _fallback.BindModelAsync(ctx);

        var raw = valueResult.FirstValue;
        if (string.IsNullOrWhiteSpace(raw))
        {
            ctx.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        // Normalise: replace comma with dot so both "10,5" and "10.5" parse correctly.
        var normalised = raw.Replace(',', '.');
        if (decimal.TryParse(normalised, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            ctx.Result = ModelBindingResult.Success(value);
        }
        else
        {
            ctx.ModelState.TryAddModelError(ctx.ModelName,
                $"The value '{raw}' is not a valid number.");
        }
        return Task.CompletedTask;
    }
}
