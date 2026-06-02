// DWS dashboard charts built on D3 v7 (global `d3`). Exposes window.DwsCharts.
// Brand palette mirrors wwwroot/css/dws.css :root tokens — no generic/Tailwind colours.
(function () {
    "use strict";
    var PALETTE = ["#003d7a", "#0066b3", "#00838f", "#2e7d32", "#c49000", "#c62828", "#7db8e0", "#8892a0"];

    // data: [{ Label, Value }, ...]
    function barChart(selector, data, opts) {
        opts = opts || {};
        var el = document.querySelector(selector);
        if (!el || !window.d3) return;
        el.innerHTML = "";
        var width = el.clientWidth || 480, height = el.clientHeight || 220;
        var m = { top: 10, right: 10, bottom: 40, left: 40 };
        var svg = d3.select(el).append("svg").attr("width", width).attr("height", height);

        var x = d3.scaleBand().domain(data.map(function (d) { return d.Label; }))
            .range([m.left, width - m.right]).padding(0.25);
        var y = d3.scaleLinear().domain([0, d3.max(data, function (d) { return d.Value; }) || 1]).nice()
            .range([height - m.bottom, m.top]);

        svg.append("g").attr("transform", "translate(0," + (height - m.bottom) + ")")
            .call(d3.axisBottom(x)).selectAll("text").style("font-size", "11px");
        svg.append("g").attr("transform", "translate(" + m.left + ",0)").call(d3.axisLeft(y).ticks(5));

        svg.selectAll("rect.bar").data(data).enter().append("rect").attr("class", "bar")
            .attr("x", function (d) { return x(d.Label); })
            .attr("y", function (d) { return y(d.Value); })
            .attr("width", x.bandwidth())
            .attr("height", function (d) { return (height - m.bottom) - y(d.Value); })
            .attr("rx", 3)
            .attr("fill", function (d, i) { return (opts.colors || PALETTE)[i % (opts.colors || PALETTE).length]; });

        svg.selectAll("text.val").data(data).enter().append("text").attr("class", "val")
            .attr("x", function (d) { return x(d.Label) + x.bandwidth() / 2; })
            .attr("y", function (d) { return y(d.Value) - 4; })
            .attr("text-anchor", "middle").style("font-size", "11px").style("fill", "#1a1f2b")
            .text(function (d) { return d.Value; });
    }

    // data: [{ Label, Value }, ...]
    function donutChart(selector, data, opts) {
        opts = opts || {};
        var el = document.querySelector(selector);
        if (!el || !window.d3) return;
        el.innerHTML = "";
        var width = el.clientWidth || 320, height = el.clientHeight || 240;
        var radius = Math.min(width, height) / 2 - 10;
        var svg = d3.select(el).append("svg").attr("width", width).attr("height", height)
            .append("g").attr("transform", "translate(" + width / 2 + "," + height / 2 + ")");

        var pie = d3.pie().value(function (d) { return d.Value; }).sort(null);
        var arc = d3.arc().innerRadius(radius * 0.55).outerRadius(radius);
        var color = function (i) { return (opts.colors || PALETTE)[i % (opts.colors || PALETTE).length]; };

        svg.selectAll("path").data(pie(data)).enter().append("path")
            .attr("d", arc).attr("fill", function (d, i) { return color(i); })
            .attr("stroke", "#fff").style("stroke-width", "2px");

        var legend = d3.select(el).append("div").style("font-size", "11px").style("margin-top", "6px");
        data.forEach(function (d, i) {
            // Build swatch + label separately; label via .text() so it is never interpreted as
            // HTML (defensive — keeps donutChart safe if ever fed labels from a less-trusted source).
            var item = legend.append("span").style("display", "inline-block").style("margin-right", "12px");
            item.append("span").style("display", "inline-block").style("width", "10px")
                .style("height", "10px").style("background", color(i)).style("margin-right", "4px");
            item.append("span").text(d.Label + " (" + d.Value + ")");
        });
    }

    window.DwsCharts = { barChart: barChart, donutChart: donutChart };
})();
