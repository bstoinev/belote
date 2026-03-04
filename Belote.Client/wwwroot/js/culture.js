window.beloteCulture = {
  get: function () {
    try {
      return localStorage.getItem("belote.culture");
    } catch {
      return null;
    }
  },
  set: function (value) {
    try {
      localStorage.setItem("belote.culture", value);
    } catch {
      /* ignore */
    }
  },
  reload: function () {
    location.reload();
  }
};

