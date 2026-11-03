mergeInto(LibraryManager.library, {
  // Unity 웹 빌드는 비밀번호 칸이면 키보드 종류(NumberPad)를 무시하고 type="password" 만 준다(_JS_MobileKeyboard_Show).
  // 우리 비밀번호는 숫자 4~6자리뿐이라, password 가 되는 순간 숫자 키패드를 쓰라고 알려 준다.
  // Unity 는 type 을 정한 뒤 focus 해서 키보드를 띄우므로 그 전에 붙는다.
  // ponytail: 페이지의 모든 input 에 걸린다. 지금 입력칸은 Unity 폰 키보드 하나뿐이라 괜찮고, 숫자가 아닌 비밀번호 칸이 생기면 다시 본다
  WebKeyboard_NumericPassword: function () {
    var desc = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'type');
    if (!desc || !desc.set) {
      return;
    }
    Object.defineProperty(HTMLInputElement.prototype, 'type', {
      configurable: true,
      enumerable: desc.enumerable,
      get: desc.get,
      set: function (value) {
        desc.set.call(this, value);
        if (value === 'password') {
          this.setAttribute('inputmode', 'numeric');
          this.setAttribute('pattern', '[0-9]*');
        } else {
          this.removeAttribute('inputmode');
          this.removeAttribute('pattern');
        }
      },
    });
  },
});
