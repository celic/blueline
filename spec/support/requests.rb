# frozen_string_literal: true
module Support
  module Requests

    def app
      described_class
    end

    def json
      JSON.parse(last_response.body).deep_symbolize_keys
    end

    def expect_status(status)
      expect(last_response.status).to eql status
    end

    def user_agent
      'Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/48.0.2564.109 Safari/537.36'
    end

  end
end
