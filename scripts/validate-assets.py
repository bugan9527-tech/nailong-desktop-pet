"""Independent PNG/GIF delivery checks. Requires Pillow and NumPy; not needed to run the pet."""
from pathlib import Path
import json
import sys
import numpy as np
from PIL import Image

root = Path(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).resolve().parents[1])
manifest = json.loads((root / 'assets/animations/animations.json').read_text(encoding='utf-8-sig'))
clips = {item['id']: item for item in manifest}
checks = []
def check(value, message):
    if not value:
        raise AssertionError(message)
    checks.append(message)
def pixels(action, index):
    return np.array(Image.open(root / 'assets/animations' / clips[action]['frames'][index]).convert('RGBA'))

check(set(clips) == {'idle', 'blink', 'wave', 'tilt', 'pet', 'belly'}, 'six runtime actions')
check(len(clips['belly']['frames']) == 78 and clips['belly']['fps'] == 30 and not clips['belly']['loop'], '78 belly frames at 30 fps as a one-shot')
check(len(clips['pet']['frames']) == 174 and clips['pet']['fps'] == 30, '174 frames at 30 fps')
check(len(clips['pet']['frames']) / clips['pet']['fps'] == 5.8, '5800 ms runtime timeline')
neutral = pixels('idle', 0)
check(np.array_equal(neutral, np.array(Image.open(root / 'assets/reference/calibrated/front.png'))), 'calibrated front is the runtime neutral')
for action in clips:
    check(np.array_equal(pixels(action, 0), neutral) and np.array_equal(pixels(action, len(clips[action]['frames']) - 1), neutral), f'{action} begins and ends at neutral')
    for file in clips[action]['frames']:
        image = Image.open(root / 'assets/animations' / file)
        check(image.size == (512, 512) and image.mode == 'RGBA', f'{file} has transparent 512px canvas')
for action in ['blink', 'wave', 'tilt']:
    check(all(np.array_equal(pixels(action, i)[440:], neutral[440:]) for i in range(8)), f'{action} retains calibrated legs and scale')
belly_report = json.loads((root / 'artifacts/belly/continuity-validation.json').read_text(encoding='utf-8-sig'))
check(len(belly_report['frames']) == 78 and belly_report['durationMs'] == 2600, 'belly timeline has continuity records for all 78 frames')
for i in range(78):
    frame = pixels('belly', i)
    check(np.array_equal(frame[:160], neutral[:160]) and np.array_equal(frame[430:], neutral[430:]), f'belly/{i:03} preserves the face and planted feet')
    check(not frame[:40, :, 3].any() and not frame[:, :40, 3].any() and not frame[:, -40:, 3].any(), f'belly/{i:03} has transparent empty margins')
check(not np.array_equal(pixels('belly', 30)[250:370], neutral[250:370]), 'belly held pose differs from relaxed arms')
check(np.array_equal(pixels('belly', 30), pixels('belly', 51)), 'belly hands remain steady during the pause')
check(max(f['adjacentMeanPixelChange'] for f in belly_report['frames']) < 8, 'belly transition has no abrupt frame jumps')
belly_preview = Image.open(root / 'assets/reference/belly-click/收手护肚.gif')
belly_duration = sum(belly_preview.seek(i) is None and belly_preview.info.get('duration', 0) for i in range(belly_preview.n_frames))
check(belly_preview.n_frames == 78 and belly_duration == 2600, 'belly GIF has 78 frames and 2600 ms')
check(not np.array_equal(pixels('pet', 90)[320:], neutral[320:]), 'turning changes lower body and feet')
check(not np.array_equal(pixels('pet', 63), pixels('pet', 81)), 'side-down and hands-to-belly are distinct poses')
report = json.loads((root / 'artifacts/pet/continuity-validation.json').read_text(encoding='utf-8-sig'))
check(len(report['frames']) == 174, 'every reaction frame has a continuity record')
check([item['sourceFrame'] for item in report['frames'][9:150]] == [frame for frame in range(47) for _ in range(3)], '47 original reaction frames retain 100 ms each')
baselines = [item['footBaseline'] for item in report['frames']]
check(min(baselines) >= 465 and max(baselines) <= 474, 'foot baseline remains within five pixels of anchor')
source = Image.open(root / 'assets/reference/original/original.gif')
source_duration = sum(source.seek(i) is None and source.info.get('duration', 0) for i in range(source.n_frames))
check(source.n_frames == 47 and source_duration == 4700, 'original GIF is 47 frames and 4700 ms')
preview = Image.open(root / 'assets/reference/pet/摸头-连贯全身.gif')
duration = sum(preview.seek(i) is None and preview.info.get('duration', 0) for i in range(preview.n_frames))
check(preview.n_frames == 174 and duration == 5800, 'exported GIF is 174 frames and 5800 ms')
result = {'success': True, 'checkCount': len(checks), 'runtimeFrames': sum(len(c['frames']) for c in clips.values()), 'petFrames': 174, 'durationMs': duration, 'footBaselineRange': [min(baselines), max(baselines)], 'maxAdjacentMeanPixelChange': max(item['adjacentMeanPixelChange'] for item in report['frames']), 'bellyFrames': 78, 'bellyDurationMs': belly_duration, 'bellyFootBaselineRange': [min(item['footBaseline'] for item in belly_report['frames']), max(item['footBaseline'] for item in belly_report['frames'])], 'bellyMaxAdjacentMeanPixelChange': max(item['adjacentMeanPixelChange'] for item in belly_report['frames']), 'checks': checks}
(root / 'artifacts/delivery-validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'checks'}, ensure_ascii=False, indent=2))
