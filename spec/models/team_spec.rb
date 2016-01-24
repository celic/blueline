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
#  division_id  :integer
#  wins         :integer          default(0)
#  losses       :integer          default(0)
#  ot           :integer          default(0)
#  sol          :integer          default(0)
#  points       :integer          default(0)
#  sow          :integer          default(0)
#
# Indexes
#
#  index_teams_on_abbreviation  (abbreviation)
#

require 'rails_helper'

RSpec.describe Team, type: :model do
  pending "add some examples to (or delete) #{__FILE__}"
end
