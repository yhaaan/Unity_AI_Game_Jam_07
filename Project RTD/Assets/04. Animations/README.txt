Animator Controller와 Animation Clip.
대상별 2단계 폴더로 분류한다.
작업 원본은 90. ArtShare 아래에 둔다.

PlayerLayers.yaml은 Chimera/Resources/ChimeraParts/manifest.json에서 rabbit, lion, bear, axolotl, duck의 레이어와 미리보기를 임포트한다. 원본 수정 후 Reimport한다. 플레이어 빌드에서는 외부 파일을 읽지 않는다.
DroneEnemy.yaml과 ScientistEnemy.yaml은 Chimera/Resources/ChimeraEnemies에서 드론 및 ray 과학자의 정지 스프라이트를 합성한다. 재임포트에는 해당 원본이 필요하다.
Tools/RTD/Assign matching farm part art: 15개 SO와 플레이어 미리보기를 갱신한다. Tools/RTD/Install Stage01 enemies: 누락된 적 프리팹과 스테이지 인스턴스를 추가한다. 적의 Matching Heads는 드론의 귀환 대상, 과학자를 쓰러뜨릴 수 있는 머리 목록이다. 비어 있으면 발동하지 않는다.
