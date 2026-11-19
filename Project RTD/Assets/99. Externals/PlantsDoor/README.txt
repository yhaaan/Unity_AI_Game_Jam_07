식물 16종 + 실험실 문 애니메이션
================================

식물 (부위를 나눠서 움직임)
  풀/덤불 계열 (grass-tall, bush, grass-low, grass-wild, grass-clover, grass-dry)
    한 덩어리를 줄 단위로 휘게 해서 뿌리는 고정, 끝으로 갈수록 크게 흔들리고 잎이 벌어졌다 오므라듦
  wheat     풀 + 이삭(이삭이 줄기 끝을 따라가며 끄덕임)
  clover    가운데/왼쪽/오른쪽 줄기 3개가 시차를 두고 흔들림
  꽃 계열   줄기(휘어짐) + 잎(좌우 팔랑) + 꽃머리(줄기 끝에 붙어 기울고 흔들림)
            + 눈(깜빡임), 혀(날름), 코(킁킁), 이빨(달달), 귀(펄럭) 를 각각 따로
    flower-pink, flower-buck, bellflower(종 2개가 매달려 흔들림), calla-tongue,
    flower-nose, flower-tooth, fern-eyes(고사리 2줄기 + 눈), pitcher-ear
  반응: 몸이 스치고 지나가면 그 방향으로 휘청 → 스프링처럼 출렁이며 돌아옴

문 (lab door)
  닫힘   셔터 내려옴, 램프 노랑, 막는 콜라이더 켜짐
  열림중 살짝 내려앉았다가 위로 말려 올라가며 살짝 튕김, 바닥 먼지, 램프 깜빡
  열림   램프 초록, 콜라이더 꺼짐
  닫힘중 중력처럼 떨어져 쾅 → 찌그러짐 + 흔들림 + 양옆 먼지
  (먼지는 테두리 없는 둥근 한 가지 색)

웹 미리보기
  web 폴더에서  python3 -m http.server 8768  →  http://localhost:8768/index.html
  마우스로 식물을 쓸면 반응, 클릭은 크게, 문은 버튼/클릭

Unity  (Unity/PlantsDoor 폴더를 Assets 안에 넣기)
  식물: 빈 GameObject에 PlantRig → plantName 에 위 이름. 오브젝트 위치 = 식물 뿌리(바닥 가운데)
        wind(바람 세기), period(주기), sortingLayerName/sortingOrder
        reactToBodies 켜면 Rigidbody2D 가 지나갈 때 자동 반응 (pokeTag 로 플레이어만 가능)
        코드로: GetComponent<PlantRig>().Poke(+1 또는 -1, 세기)
  문:   빈 GameObject에 LabDoor. 오브젝트 위치 = 문 바닥 가운데
        Open() / Close() / Toggle(), StateChanged 이벤트
        autoOpen 켜면 playerTag("Player") 가 가까이 오면 열리고 나가면 closeDelay 뒤 닫힘
  스프라이트 임포트 설정은 Editor/PlantSpriteImporter 가 자동으로 맞춤

다시 만들기 (_tools)
  segment.py        원본 스프라이트 → 부위 PNG + rig.json  (식물별 규칙은 파일 위쪽 PLANTS 표)
  door.py           문틀 개구부/램프 위치 측정
  package_unity.py  web → Unity/PlantsDoor/Resources 복사

벽 (Resources/Walls)
  JumpWall   점프벽 기둥. style = LabBarrier(연구소 기둥) / MossyCliff(이끼 절벽), width·height(월드 단위)
             뚜껑과 리벳 부분은 그대로, 가운데만 늘어나서 어떤 높이도 가능. 위에 착지하면 뚜껑이 찌그러졌다 튕김
  BrickWall  부서지는 벽돌벽. Hit() 1번 = 금 감(흔들림+먼지), 2번 = 벽돌이 돌며 날아가 튕기고 부서짐 → 바닥에 잔해
             hitsToBreak, StateChanged 이벤트, Reset()

게임 적용 (ProjectRTD.Gameplay/StageDressing.cs)
  스테이지가 열릴 때 자동으로 꾸밈 (씬 파일은 안 건드림)
    골(StageGoal)        → 깃대/깃발 숨기고 실험실 문. 플레이어가 2.4 안으로 오면 열리고 멀어지면 닫힘
    "Jump Wall"          → 연구소 기둥 그림 (원래 크기 그대로)
    스테이지는 실내 연구실이라 식물/자연물 꾸밈은 하지 않음

섬 발판 (Resources/Islands)  자연 섬 8종. 스테이지에서는 쓰지 않음
  (스테이지 바닥은 03. Sprites/Background(New)/floor.png 의 floor_83, StageCourseLayout 이 깔고
   양끝은 보이지 않는 벽 + 카메라 경계(StageOverviewCamera)로 처리)
  islands.json: size(px), top = 윗면(밟는 면) 외곽선이 시작하는 줄(px). 100 PPU, 가운데 피벗
  배치: 스프라이트 위치 y = 바닥 콜라이더 윗면 - (top - size.y/2)/100 * scale
