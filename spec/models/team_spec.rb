# == Schema Information
#
# Table name: teams
#
#  id           :integer          not null, primary key
#  name         :string
#  abbreviation :string
#  city         :string
#  state        :string
#  full_name    :string
#

require 'rails_helper'

RSpec.describe Team, type: :model do
  pending "add some examples to (or delete) #{__FILE__}"
end
