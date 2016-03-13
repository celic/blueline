# frozen_string_literal: true
# This file is copied to spec/ when you run 'rails generate rspec:install'
ENV['RAILS_ENV'] ||= 'test'
require File.expand_path('../../config/environment', __FILE__)

abort 'The Rails environment is running in production mode!' if Rails.env.production?

require 'spec_helper'
require 'database_cleaner'

ActiveRecord::Migration.maintain_test_schema!

Dir[File.dirname(__FILE__) + '/support/*.rb'].each { |file| require file }

RSpec.configure do |config|

  config.include FactoryGirl::Syntax::Methods

  config.include Rack::Test::Methods, type: :request, file_path: %r{spec/api}
  config.include Support::Requests, file_path: %r{spec/api}

  config.before(:suite) do
    DatabaseCleaner.strategy = :transaction
    DatabaseCleaner.clean_with :truncation
  end

  config.around(:each) do |example|
    DatabaseCleaner.cleaning do
      example.run
    end
  end
end
