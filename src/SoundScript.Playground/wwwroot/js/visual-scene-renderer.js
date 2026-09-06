// Canonical browser presentation adapter for SoundScript temporal visual scenes.
// The scene has already been evaluated from VisualTimeline.StateAt(t) by the
// Media layer. This file only paints supplied logical 1280 x 720 primitives;
// it owns no timeline, automation, frame rate, or playback clock.
window.SoundScriptVisualRenderer = (function () {
    const designWidth = 1280;
    const designHeight = 720;

    function clamp(value, minimum, maximum) {
        return Math.max(minimum, Math.min(maximum, value));
    }

    function number(value, fallback) {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    function contextFor(canvasOrContext) {
        if (canvasOrContext && typeof canvasOrContext.getContext === 'function') {
            const context = canvasOrContext.getContext('2d');
            if (!context) {
                throw new Error('The visual scene canvas does not provide a 2D context.');
            }
            return context;
        }

        if (canvasOrContext && canvasOrContext.canvas && typeof canvasOrContext.fillRect === 'function') {
            return canvasOrContext;
        }

        throw new Error('SoundScriptVisualRenderer.renderScene requires a canvas or 2D context.');
    }

    function roundedRect(context, left, top, width, height, radius) {
        const resolvedRadius = Math.min(Math.max(0, radius), width / 2, height / 2);
        context.beginPath();
        context.moveTo(left + resolvedRadius, top);
        context.lineTo(left + width - resolvedRadius, top);
        context.quadraticCurveTo(left + width, top, left + width, top + resolvedRadius);
        context.lineTo(left + width, top + height - resolvedRadius);
        context.quadraticCurveTo(left + width, top + height, left + width - resolvedRadius, top + height);
        context.lineTo(left + resolvedRadius, top + height);
        context.quadraticCurveTo(left, top + height, left, top + height - resolvedRadius);
        context.lineTo(left, top + resolvedRadius);
        context.quadraticCurveTo(left, top, left + resolvedRadius, top);
        context.closePath();
    }

    function drawBackground(context, width, height) {
        context.save();
        context.setTransform(1, 0, 0, 1, 0, 0);

        const background = context.createLinearGradient(0, 0, width, height);
        background.addColorStop(0, '#10192a');
        background.addColorStop(0.55, '#17122c');
        background.addColorStop(1, '#0e1c26');
        context.fillStyle = background;
        context.fillRect(0, 0, width, height);

        const greenGlow = context.createRadialGradient(
            width * 0.75, height * 0.15, 0,
            width * 0.75, height * 0.15, Math.max(width, height) * 0.3);
        greenGlow.addColorStop(0, 'rgba(110, 231, 183, 0.18)');
        greenGlow.addColorStop(1, 'rgba(110, 231, 183, 0)');
        context.fillStyle = greenGlow;
        context.fillRect(0, 0, width, height);

        const indigoGlow = context.createRadialGradient(
            width * 0.18, height * 0.9, 0,
            width * 0.18, height * 0.9, Math.max(width, height) * 0.38);
        indigoGlow.addColorStop(0, 'rgba(129, 140, 248, 0.25)');
        indigoGlow.addColorStop(1, 'rgba(129, 140, 248, 0)');
        context.fillStyle = indigoGlow;
        context.fillRect(0, 0, width, height);

        const cell = Math.max(12, Math.round(Math.min(width / designWidth, height / designHeight) * 32));
        context.strokeStyle = 'rgba(255, 255, 255, 0.07)';
        context.lineWidth = 1;
        context.globalAlpha = 0.28;
        context.beginPath();
        for (let x = 0; x <= width; x += cell) {
            context.moveTo(x + 0.5, 0);
            context.lineTo(x + 0.5, height);
        }
        for (let y = 0; y <= height; y += cell) {
            context.moveTo(0, y + 0.5);
            context.lineTo(width, y + 0.5);
        }
        context.stroke();
        context.restore();
    }

    function drawCenteredText(context, label, left, top, width, height, font, color) {
        context.fillStyle = color;
        context.font = font;
        context.textAlign = 'center';
        context.textBaseline = 'middle';
        context.fillText(label, left + width / 2, top + height / 2, Math.max(0, width - 18));
    }

    function drawIntro(context, primitive) {
        context.save();
        context.shadowColor = 'rgba(0, 0, 0, 0.24)';
        context.shadowBlur = 38;
        context.shadowOffsetY = 14;
        roundedRect(context, primitive.left, primitive.top, primitive.width, primitive.height, primitive.height / 2);
        context.fillStyle = 'rgba(15, 24, 43, 0.72)';
        context.fill();
        context.shadowColor = 'transparent';
        context.strokeStyle = 'rgba(255, 255, 255, 0.4)';
        context.lineWidth = 2;
        context.stroke();
        drawCenteredText(
            context,
            primitive.label || 'A visual idea begins',
            primitive.left,
            primitive.top,
            primitive.width,
            primitive.height,
            `800 ${Math.max(22, Math.min(42, primitive.height * 0.48))}px system-ui, sans-serif`,
            '#eef6ff');
        context.restore();
    }

    function drawCircle(context, primitive) {
        const centerX = primitive.left + primitive.width / 2;
        const centerY = primitive.top + primitive.height / 2;
        const radiusX = primitive.width / 2;
        const radiusY = primitive.height / 2;

        context.save();
        context.shadowColor = 'rgba(0, 0, 0, 0.38)';
        context.shadowBlur = 40;
        context.shadowOffsetY = 22;
        context.beginPath();
        context.ellipse(centerX, centerY, radiusX, radiusY, 0, 0, Math.PI * 2);
        const gradient = context.createRadialGradient(
            primitive.left + primitive.width * 0.35,
            primitive.top + primitive.height * 0.3,
            Math.max(1, Math.min(radiusX, radiusY) * 0.05),
            centerX,
            centerY,
            Math.max(radiusX, radiusY));
        gradient.addColorStop(0, '#d9fff0');
        gradient.addColorStop(0.28, '#6ee7b7');
        gradient.addColorStop(0.7, '#4f46e5');
        gradient.addColorStop(1, '#1e1b4b');
        context.fillStyle = gradient;
        context.fill();
        context.shadowColor = 'transparent';
        context.strokeStyle = 'rgba(255, 255, 255, 0.64)';
        context.lineWidth = 2;
        context.stroke();

        context.beginPath();
        context.ellipse(centerX, centerY, radiusX + 10, radiusY + 10, 0, 0, Math.PI * 2);
        context.strokeStyle = 'rgba(110, 231, 183, 0.18)';
        context.lineWidth = 16;
        context.stroke();

        if (primitive.label) {
            drawCenteredText(
                context,
                primitive.label,
                primitive.left,
                primitive.top,
                primitive.width,
                primitive.height,
                `700 ${Math.max(28, Math.min(64, Math.min(primitive.width, primitive.height) * 0.38))}px system-ui, sans-serif`,
                'rgba(255, 255, 255, 0.75)');
        }
        context.restore();
    }

    function drawProduct(context, primitive) {
        context.save();
        context.shadowColor = 'rgba(0, 0, 0, 0.34)';
        context.shadowBlur = 40;
        context.shadowOffsetY = 20;
        roundedRect(context, primitive.left, primitive.top, primitive.width, primitive.height, 14);
        const gradient = context.createLinearGradient(
            primitive.left,
            primitive.top,
            primitive.left + primitive.width,
            primitive.top + primitive.height);
        gradient.addColorStop(0, '#fef3c7');
        gradient.addColorStop(0.4, '#fbbf24');
        gradient.addColorStop(1, '#fb7185');
        context.fillStyle = gradient;
        context.fill();
        context.shadowColor = 'transparent';
        context.strokeStyle = 'rgba(255, 255, 255, 0.6)';
        context.lineWidth = 2;
        context.stroke();
        drawCenteredText(
            context,
            primitive.label || 'PRODUCT',
            primitive.left,
            primitive.top,
            primitive.width,
            primitive.height,
            `900 ${Math.max(20, Math.min(42, primitive.height * 0.3))}px system-ui, sans-serif`,
            '#071b17');
        context.restore();
    }

    function drawSparkle(context, primitive) {
        context.save();
        context.shadowColor = 'rgba(251, 191, 36, 0.95)';
        context.shadowBlur = 20;
        drawCenteredText(
            context,
            primitive.label || '✦',
            primitive.left,
            primitive.top,
            primitive.width,
            primitive.height,
            `${Math.max(34, Math.min(96, Math.max(primitive.width, primitive.height) * 0.85))}px system-ui, sans-serif`,
            '#fef3c7');
        context.restore();
    }

    function drawGeneric(context, primitive) {
        context.save();
        roundedRect(context, primitive.left, primitive.top, primitive.width, primitive.height, 8);
        context.fillStyle = 'rgba(15, 19, 26, 0.8)';
        context.fill();
        context.strokeStyle = '#a5f3fc';
        context.lineWidth = 2;
        context.stroke();
        drawCenteredText(
            context,
            primitive.label || primitive.name || 'visual',
            primitive.left,
            primitive.top,
            primitive.width,
            primitive.height,
            `700 ${Math.max(18, Math.min(32, primitive.height * 0.36))}px system-ui, sans-serif`,
            '#e8ecf4');
        context.restore();
    }

    function normalizedPrimitive(primitive) {
        const width = Math.max(1, number(primitive && primitive.width, 160));
        const height = Math.max(1, number(primitive && primitive.height, 72));
        return {
            name: String((primitive && primitive.name) || ''),
            kind: String((primitive && primitive.kind) || 'generic').toLowerCase(),
            label: primitive && primitive.label == null ? '' : String((primitive && primitive.label) || ''),
            left: number(primitive && primitive.left, (designWidth - width) / 2),
            top: number(primitive && primitive.top, (designHeight - height) / 2),
            width,
            height,
            opacity: clamp(number(primitive && primitive.opacity, 1), 0, 1),
            rotationDegrees: number(primitive && primitive.rotationDegrees, 0)
        };
    }

    function drawPrimitive(context, primitive) {
        if (primitive && Array.isArray(primitive.paths)) {
            // Positions and rotation have already been resolved by the shared geometry builder.
            context.save();
            context.globalAlpha = clamp(number(primitive.opacity, 1), 0, 1);
            context.lineJoin = 'round';
            context.lineCap = 'round';
            for (const path of primitive.paths) {
                if (!path.points || path.points.length === 0) continue;
                context.beginPath();
                context.moveTo(path.points[0].x, path.points[0].y);
                for (let i = 1; i < path.points.length; i++)
                    context.lineTo(path.points[i].x, path.points[i].y);
                if (path.closed) context.closePath();
                if (path.closed && path.fill !== 'none') {
                    context.fillStyle = path.fill;
                    context.fill();
                }
                if (path.stroke !== 'none' && path.strokeWidth > 0) {
                    context.strokeStyle = path.stroke;
                    context.lineWidth = path.strokeWidth;
                    context.stroke();
                }
            }
            context.restore();
            return;
        }
        const normalized = normalizedPrimitive(primitive);
        if (normalized.opacity <= 0) {
            return;
        }

        context.save();
        context.globalAlpha = normalized.opacity;
        const centerX = normalized.left + normalized.width / 2;
        const centerY = normalized.top + normalized.height / 2;
        context.translate(centerX, centerY);
        context.rotate(normalized.rotationDegrees * Math.PI / 180);
        context.translate(-centerX, -centerY);

        switch (normalized.kind) {
        case 'intro':
            drawIntro(context, normalized);
            break;
        case 'circle':
            drawCircle(context, normalized);
            break;
        case 'product':
            drawProduct(context, normalized);
            break;
        case 'sparkle':
            drawSparkle(context, normalized);
            break;
        default:
            drawGeneric(context, normalized);
            break;
        }

        context.restore();
    }

    function renderScene(canvasOrContext, scene) {
        const context = contextFor(canvasOrContext);
        const canvas = context.canvas;
        const width = Math.max(1, number(canvas.width, designWidth));
        const height = Math.max(1, number(canvas.height, designHeight));
        const primitives = scene && Array.isArray(scene.primitives) ? scene.primitives : [];

        context.save();
        drawBackground(context, width, height);
        context.setTransform(width / designWidth, 0, 0, height / designHeight, 0, 0);
        for (const primitive of primitives) {
            drawPrimitive(context, primitive);
        }
        context.restore();
    }

    return {
        designWidth,
        designHeight,
        renderScene
    };
})();
