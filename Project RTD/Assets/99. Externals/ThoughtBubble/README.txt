생각 말풍선 (ThoughtBubble)
  빈 GameObject에 ThoughtBubble 붙이기. 오브젝트 위치 = 꼬리 끝(가장 작은 동그라미) → 캐릭터 머리 위에 두면 됨
  또는 follow 에 캐릭터 Transform, followOffset 으로 머리 위 위치
  코드: bubble.Show("오리발이면 건널 수 있을까?");  bubble.Hide();
  size 0 이면 글자 길이에 맞춰 자동 크기 (가운데 혹이 반복되며 길어짐), 직접 지정도 가능(원본 px 단위)
  tail Right/Left/None, scale(월드 크기), fontSize(원본 px 기준), autoHideAfter(초), bob(둥실 세기)
  * 기본 TMP 폰트(Engcomica)에는 한글이 없음 → font 칸에 한글 TMP 폰트를 넣어야 한글이 보임
