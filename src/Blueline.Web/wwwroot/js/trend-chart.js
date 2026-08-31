// Thin wrapper over Chart.js. Blazor owns the data; this file owns nothing but the canvas.
// Chart.js itself is loaded as a global by a plain script tag in App.razor.

const charts = new Map();

const GRID = 'rgba(148, 163, 184, 0.14)';
const TEXT = '#94a3b8';

export function render(canvasId, spec) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || !window.Chart) return;

    // Chart.js keeps its own state per canvas; replacing data means tearing the old one down.
    destroy(canvasId);

    const datasets = spec.datasets.map(d => ({
        label: d.label,
        // A time axis needs each point to carry its own date; a category axis aligns by index.
        data: spec.timeAxis && d.dates
            ? d.dates.map((date, i) => ({ x: date, y: d.data[i] }))
            : d.data,
        type: d.kind === 'bar' ? 'bar' : 'line',
        borderColor: d.color,
        backgroundColor: d.kind === 'bar' ? withAlpha(d.color, 0.45) : withAlpha(d.color, 0.12),
        borderWidth: d.kind === 'bar' ? 0 : 2,
        pointStyle: d.pointStyle ?? 'circle',
        // Markers only where they identify something: a chart of several subjects, where colour
        // was otherwise the only thing telling the lines apart. Drawn every few points rather
        // than on all eighty-two, which would read as a dotted line instead of a shape.
        pointRadius: spec.markers && d.kind !== 'bar' ? markerRadius(d.data.length) : 0,
        pointHoverRadius: 4,
        pointHitRadius: 12,
        tension: 0.25,
        fill: d.fill === true,
        // A dashed line reads as "derived" next to the solid raw series.
        borderDash: d.dashed ? [5, 4] : undefined,
        spanGaps: true,
        order: d.kind === 'bar' ? 2 : 1,
    }));

    const chart = new window.Chart(canvas, {
        // Labels must be withheld from a time axis. Chart.js would otherwise try to parse the
        // game numbers "1".."82" as dates, producing a scale running from the year 1000 to 6500
        // and collapsing every point onto the same pixel.
        data: { labels: spec.timeAxis ? undefined : spec.labels, datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 300 },
            interaction: { mode: 'index', intersect: false },
            scales: {
                x: spec.timeAxis
                    ? {
                        // A real time scale, not date-formatted category labels. The distinction
                        // is the whole point: only proportional spacing shows a layoff, the gap
                        // between playoff rounds, or the week before the postseason starts.
                        type: 'time',
                        time: { unit: 'month', tooltipFormat: 'MMM d, yyyy' },
                        title: { display: true, text: spec.xLabel ?? 'Date', color: TEXT },
                        grid: { color: GRID, drawTicks: false },
                        ticks: { color: TEXT, maxRotation: 0, autoSkipPadding: 24 },
                    }
                    : {
                        title: { display: true, text: spec.xLabel ?? 'Game', color: TEXT },
                        grid: { color: GRID, drawTicks: false },
                        ticks: {
                            color: TEXT,
                            maxRotation: 0,
                            autoSkipPadding: 24,
                        },
                    },
                y: {
                    title: { display: true, text: spec.yLabel ?? '', color: TEXT },
                    grid: { color: GRID, drawTicks: false },
                    ticks: { color: TEXT, precision: spec.precise ? 2 : 0 },
                    beginAtZero: spec.beginAtZero !== false,
                },
            },
            plugins: {
                legend: {
                    display: datasets.length > 1,
                    // Each dataset's own shape rather than a uniform line, so the legend is the
                    // key to the markers on the chart.
                    labels: {
                        color: TEXT,
                        usePointStyle: true,
                        pointStyle: spec.markers ? undefined : 'line',
                        boxWidth: 24,
                    },
                },
                tooltip: {
                    backgroundColor: '#0f172a',
                    borderColor: 'rgba(148, 163, 184, 0.25)',
                    borderWidth: 1,
                    titleColor: '#e2e8f0',
                    bodyColor: '#cbd5e1',
                    padding: 10,
                    callbacks: {
                        // The opponent matters more than the game number when reading a spike.
                        // On a time axis the series no longer share an index, so fall back to
                        // Chart.js's own formatted date rather than mislabelling a point.
                        title: items => spec.timeAxis
                            ? items[0].label
                            : (spec.subtitles?.[items[0].dataIndex] ?? items[0].label),
                    },
                },
            },
        },
    });

    charts.set(canvasId, chart);
}

export function destroy(canvasId) {
    const existing = charts.get(canvasId);
    if (existing) {
        existing.destroy();
        charts.delete(canvasId);
    }
}

// A scriptable radius: visible on roughly every tenth point, hidden on the rest. Chart.js has no
// "every Nth marker" option, and a marker on every game turns the line into a bead necklace.
function markerRadius(pointCount) {
    const step = Math.max(1, Math.round(pointCount / 10));
    return ctx => (ctx.dataIndex % step === 0 ? 3.5 : 0);
}

function withAlpha(hex, alpha) {
    const value = hex.replace('#', '');
    const r = parseInt(value.substring(0, 2), 16);
    const g = parseInt(value.substring(2, 4), 16);
    const b = parseInt(value.substring(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
